#include <windows.h>
#include <detours/detours.h>
#include <Zydis/Zydis.h>
#include <cstdint>
#include <cstdio>
#include <cstdarg>
#include <cstring>
#include <string>
#include <vector>
#include <unordered_map>

// ============================================================================
// Logging
// ============================================================================
static FILE *g_logFile = nullptr;

static void Log(const char *fmt, ...)
{
    char buf[2048];
    va_list args;
    va_start(args, fmt);
    vsnprintf(buf, sizeof(buf), fmt, args);
    va_end(args);

    OutputDebugStringA(buf);
    printf("%s", buf);
    fflush(stdout);

    if (g_logFile)
    {
        fprintf(g_logFile, "%s", buf);
        fflush(g_logFile);
    }
}

static void InitLogging()
{
    if (!AttachConsole(ATTACH_PARENT_PROCESS))
        AllocConsole();

    FILE *f;
    freopen_s(&f, "CONOUT$", "w", stdout);
    freopen_s(&f, "CONOUT$", "w", stderr);
    freopen_s(&f, "CONIN$", "r", stdin);

    char exePath[MAX_PATH];
    GetModuleFileNameA(nullptr, exePath, MAX_PATH);
    char *lastSlash = strrchr(exePath, '\\');
    if (lastSlash)
        *(lastSlash + 1) = 0;
    strcat_s(exePath, "MarinerServer.log");
    fopen_s(&g_logFile, exePath, "w");
}

// ============================================================================
// Pattern scanner
// ============================================================================
static uintptr_t g_imageBase = 0;
static uintptr_t g_textStart = 0;
static size_t g_textSize = 0;
static uintptr_t g_dataStart = 0;
static size_t g_dataSize = 0;

static void InitSections()
{
    auto *dos = (IMAGE_DOS_HEADER *)g_imageBase;
    auto *nt = (IMAGE_NT_HEADERS64 *)(g_imageBase + dos->e_lfanew);
    auto *section = IMAGE_FIRST_SECTION(nt);

    for (WORD i = 0; i < nt->FileHeader.NumberOfSections; i++, section++)
    {
        if (memcmp(section->Name, ".text", 5) == 0)
        {
            g_textStart = g_imageBase + section->VirtualAddress;
            g_textSize = section->Misc.VirtualSize;
        }
        else if (memcmp(section->Name, ".data", 5) == 0)
        {
            g_dataStart = g_imageBase + section->VirtualAddress;
            g_dataSize = section->Misc.VirtualSize;
        }
    }
}

static uintptr_t FindPattern(const uint8_t *pattern, size_t len, const char *name)
{
    const uint8_t *start = (const uint8_t *)g_textStart;
    const uint8_t *end = start + g_textSize - len;

    for (const uint8_t *p = start; p < end; p++)
    {
        if (memcmp(p, pattern, len) == 0)
        {
            Log("[Scanner] Found %s at 0x%llX\n", name, (unsigned long long)(uintptr_t)p);
            return (uintptr_t)p;
        }
    }

    Log("[Scanner] ERROR: %s not found!\n", name);
    return 0;
}

// Pattern with wildcard support: 0xCC = wildcard byte
static uintptr_t FindPatternMasked(const uint8_t *pattern, const uint8_t *mask, size_t len, const char *name)
{
    const uint8_t *start = (const uint8_t *)g_textStart;
    const uint8_t *end = start + g_textSize - len;

    for (const uint8_t *p = start; p < end; p++)
    {
        bool match = true;
        for (size_t i = 0; i < len; i++)
        {
            if (mask[i] && p[i] != pattern[i])
            {
                match = false;
                break;
            }
        }
        if (match)
        {
            Log("[Scanner] Found %s at 0x%llX\n", name, (unsigned long long)(uintptr_t)p);
            return (uintptr_t)p;
        }
    }

    Log("[Scanner] ERROR: %s not found!\n", name);
    return 0;
}

// Use Zydis to find a RIP-relative operand targeting .data section
// starting from 'start' address, scanning 'maxBytes' bytes forward
// matchIndex: 0 = first match, 1 = second match, etc.
// operandIdx: -1 = any operand, 0 = destination (store), 1 = source (load)
static uintptr_t FindRipRelativeTarget(uintptr_t start, size_t maxBytes, ZydisMnemonic mnemonic, int matchIndex = 0, int operandIdx = -1)
{
    ZydisDecoder decoder;
    ZydisDecoderInit(&decoder, ZYDIS_MACHINE_MODE_LONG_64, ZYDIS_STACK_WIDTH_64);

    ZydisDecodedInstruction instruction;
    ZydisDecodedOperand operands[ZYDIS_MAX_OPERAND_COUNT];
    size_t offset = 0;
    int found = 0;

    while (offset < maxBytes)
    {
        if (ZYAN_SUCCESS(ZydisDecoderDecodeFull(&decoder, (void *)(start + offset), maxBytes - offset, &instruction, operands)))
        {
            if (instruction.mnemonic == mnemonic)
            {
                for (int i = 0; i < instruction.operand_count; i++)
                {
                    if (operandIdx >= 0 && i != operandIdx)
                        continue;

                    if (operands[i].type == ZYDIS_OPERAND_TYPE_MEMORY && operands[i].mem.base == ZYDIS_REGISTER_RIP)
                    {
                        uintptr_t target = start + offset + instruction.length + (int64_t)operands[i].mem.disp.value;

                        if (target >= g_dataStart && target < g_dataStart + g_dataSize)
                        {
                            if (found == matchIndex)
                                return target;
                            found++;
                        }
                    }
                }
            }
            offset += instruction.length;
        }
        else
        {
            offset++;
        }
    }
    return 0;
}

// ============================================================================
// Resolved addresses (filled by scanner)
// ============================================================================
static struct
{
    // Globals
    bool *GIsClient;
    bool *GIsServer;
    bool *GIsRunningCommandlet;
    bool *GIsFirstInstance;
    void **GEngine;
    void **GMalloc; // FMalloc* pointer, vtable: [0x10]=Malloc, [0x18]=Realloc, [0x20]=Free

    // Functions
    uintptr_t PreInitPostStartupScreen;
    uintptr_t IsDedicatedServerInstance;
    uintptr_t OnConnectionStateChanged;
    uintptr_t ServerTravel;
    uintptr_t SelectAndLoadMap;
    uintptr_t AddGameSessionMangoIds;
    uintptr_t ConfigGetString;
    uintptr_t ConfigGetInt;
} Addr = {};

static bool ResolveAddresses()
{
    // --- Function patterns ---
    // PreInitPostStartupScreen: 4C 8B DC 55 41 54 49 8D AB 38 FF FF FF 48 81 EC B8 01 00 00 48
    static const uint8_t pat_PreInitPost[] = {0x4C, 0x8B, 0xDC, 0x55, 0x41, 0x54, 0x49, 0x8D, 0xAB, 0x38, 0xFF, 0xFF, 0xFF, 0x48, 0x81, 0xEC, 0xB8, 0x01, 0x00, 0x00, 0x48};
    Addr.PreInitPostStartupScreen = FindPattern(pat_PreInitPost, sizeof(pat_PreInitPost), "PreInitPostStartupScreen");

    // PreInitPreStartupScreen: 40 55 57 41 54 41 55 41 56 48 8D AC 24 A0 F2 FF FF 48 81 EC 60 0E 00 00
    static const uint8_t pat_PreInitPre[] = {0x40, 0x55, 0x57, 0x41, 0x54, 0x41, 0x55, 0x41, 0x56, 0x48, 0x8D, 0xAC, 0x24, 0xA0, 0xF2, 0xFF, 0xFF, 0x48, 0x81, 0xEC, 0x60, 0x0E, 0x00, 0x00};
    uintptr_t preInitPre = FindPattern(pat_PreInitPre, sizeof(pat_PreInitPre), "PreInitPreStartupScreen");

    // IsDedicatedServerInstance: 48 8B 41 30 48 85 C0 74 08 0F B6 80 4C 02 00 00 C3 C3
    static const uint8_t pat_IsDedicated[] = {0x48, 0x8B, 0x41, 0x30, 0x48, 0x85, 0xC0, 0x74, 0x08, 0x0F, 0xB6, 0x80, 0x4C, 0x02, 0x00, 0x00, 0xC3, 0xC3};
    Addr.IsDedicatedServerInstance = FindPattern(pat_IsDedicated, sizeof(pat_IsDedicated), "IsDedicatedServerInstance");

    // OnConnectionStateChanged: 48 83 EC 28 80 FA 0B 75 20
    static const uint8_t pat_OnConnState[] = {0x48, 0x83, 0xEC, 0x28, 0x80, 0xFA, 0x0B, 0x75, 0x20};
    Addr.OnConnectionStateChanged = FindPattern(pat_OnConnState, sizeof(pat_OnConnState), "OnConnectionStateChanged");

    // ServerTravel: 40 55 53 56 41 55 41 57 48 8D 6C 24 C9 48 81 EC A0 00 00 00 48 8B F2 4C
    static const uint8_t pat_ServerTravel[] = {0x40, 0x55, 0x53, 0x56, 0x41, 0x55, 0x41, 0x57, 0x48, 0x8D, 0x6C, 0x24, 0xC9, 0x48, 0x81, 0xEC, 0xA0, 0x00, 0x00, 0x00, 0x48, 0x8B, 0xF2, 0x4C};
    Addr.ServerTravel = FindPattern(pat_ServerTravel, sizeof(pat_ServerTravel), "ServerTravel");

    // SelectAndLoadMap: 48 89 5C 24 10 48 89 74 24 18 48 89 7C 24 20 55 41 54 41 55 41 56 41 57 48 8D AC 24 40 FE FF FF 48 81 EC C0 02 00 00
    static const uint8_t pat_SelectAndLoadMap[] = {0x48, 0x89, 0x5C, 0x24, 0x10, 0x48, 0x89, 0x74, 0x24, 0x18, 0x48, 0x89, 0x7C, 0x24, 0x20, 0x55, 0x41, 0x54, 0x41, 0x55, 0x41, 0x56, 0x41, 0x57, 0x48, 0x8D, 0xAC, 0x24, 0x40, 0xFE, 0xFF, 0xFF, 0x48, 0x81, 0xEC, 0xC0, 0x02, 0x00, 0x00};
    Addr.SelectAndLoadMap = FindPattern(pat_SelectAndLoadMap, sizeof(pat_SelectAndLoadMap), "SelectAndLoadMap");

    // AddGameSessionMangoIds: 48 8B C4 55 48 8D 68 A1 48 81 EC B0 00 00 00 48 89 58 08 48 89 70 F0 48
    static const uint8_t pat_AddGameSessionMangoIds[] = {0x48, 0x8B, 0xC4, 0x55, 0x48, 0x8D, 0x68, 0xA1, 0x48, 0x81, 0xEC, 0xB0, 0x00, 0x00, 0x00, 0x48, 0x89, 0x58, 0x08, 0x48, 0x89, 0x70, 0xF0, 0x48};
    Addr.AddGameSessionMangoIds = FindPattern(pat_AddGameSessionMangoIds, sizeof(pat_AddGameSessionMangoIds), "AddGameSessionMangoIds");

    // MarinerEngine::Init: 40 53 48 81 EC 80 00 00 00 48 89 AC 24 98 00 00 00 48 89 74 24 78 48 8B
    static const uint8_t pat_EngineInit[] = {0x40, 0x53, 0x48, 0x81, 0xEC, 0x80, 0x00, 0x00, 0x00, 0x48, 0x89, 0xAC, 0x24, 0x98, 0x00, 0x00, 0x00, 0x48, 0x89, 0x74, 0x24, 0x78, 0x48, 0x8B};
    uintptr_t engineInit = FindPattern(pat_EngineInit, sizeof(pat_EngineInit), "MarinerEngine::Init");

    // FEngineLoop::Init: 48 8B C4 48 89 58 10 48 89 70 18 48 89 78 20 55 41 56 41 57 48 8D A8 38
    static const uint8_t pat_EngineLoopInit[] = {0x48, 0x8B, 0xC4, 0x48, 0x89, 0x58, 0x10, 0x48, 0x89, 0x70, 0x18, 0x48, 0x89, 0x78, 0x20, 0x55, 0x41, 0x56, 0x41, 0x57, 0x48, 0x8D, 0xA8, 0x38};
    uintptr_t engineLoopInit = FindPattern(pat_EngineLoopInit, sizeof(pat_EngineLoopInit), "FEngineLoop::Init");

    // FConfigCacheIni::GetString: 4C 89 4C 24 20 4C 89 44 24 18 53 56 57 41 55 41 56 41 57 48 83 EC 38 4C
    static const uint8_t pat_ConfigGetString[] = {0x4C, 0x89, 0x4C, 0x24, 0x20, 0x4C, 0x89, 0x44, 0x24, 0x18, 0x53, 0x56, 0x57, 0x41, 0x55, 0x41, 0x56, 0x41, 0x57, 0x48, 0x83, 0xEC, 0x38, 0x4C};
    Addr.ConfigGetString = FindPattern(pat_ConfigGetString, sizeof(pat_ConfigGetString), "FConfigCacheIni::GetString");

    // FConfigCacheIni::GetInt: prologue + CALL GetString + post-call, with wildcard on CALL displacement
    // Pattern: 40 53 48 83 EC 40 33 C0 49 8B D9 48 89 44 24 30 4C 8D 4C 24 30 48 89 44 24 38 48 8B 44 24 70 48 89 44 24 20 E8 ?? ?? ?? ?? 84 C0 74 1E 83 7C 24 38 00
    static const uint8_t pat_ConfigGetInt[] = {0x40, 0x53, 0x48, 0x83, 0xEC, 0x40, 0x33, 0xC0, 0x49, 0x8B, 0xD9, 0x48, 0x89, 0x44, 0x24, 0x30, 0x4C, 0x8D, 0x4C, 0x24, 0x30, 0x48, 0x89, 0x44, 0x24, 0x38, 0x48, 0x8B, 0x44, 0x24, 0x70, 0x48, 0x89, 0x44, 0x24, 0x20, 0xE8, 0x00, 0x00, 0x00, 0x00, 0x84, 0xC0, 0x74, 0x1E, 0x83, 0x7C, 0x24, 0x38, 0x00};
    static const uint8_t msk_ConfigGetInt[] = {1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1};
    Addr.ConfigGetInt = FindPatternMasked(pat_ConfigGetInt, msk_ConfigGetInt, sizeof(pat_ConfigGetInt), "FConfigCacheIni::GetInt");

    // Check required patterns found
    if (!Addr.PreInitPostStartupScreen || !preInitPre || !Addr.IsDedicatedServerInstance || !Addr.OnConnectionStateChanged || !Addr.ServerTravel || !engineInit || !engineLoopInit || !Addr.ConfigGetString || !Addr.ConfigGetInt)
    {
        Log("[Scanner] ERROR: One or more required patterns not found!\n");
        return false;
    }

    // --- Resolve globals via Zydis ---

    // GIsClient/GIsServer: In PreInitPreStartupScreen, scan for two consecutive
    // MOV [rip+disp], reg instructions targeting .data, where targets are 1 byte apart.
    // These are at ~+0x1E00 into the function. Scan a wide range to be safe.
    Log("[Scanner] Resolving GIsClient/GIsServer from PreInitPreStartupScreen...\n");
    {
        ZydisDecoder decoder;
        ZydisDecoderInit(&decoder, ZYDIS_MACHINE_MODE_LONG_64, ZYDIS_STACK_WIDTH_64);
        ZydisDecodedInstruction instr;
        ZydisDecodedOperand ops[ZYDIS_MAX_OPERAND_COUNT];

        uintptr_t prevTarget = 0;
        size_t prevOffset = 0;
        size_t offset = 0;
        size_t scanSize = 0x2200;

        while (offset < scanSize)
        {
            if (ZYAN_SUCCESS(ZydisDecoderDecodeFull(&decoder, (void *)(preInitPre + offset), scanSize - offset, &instr, ops)))
            {
                if (instr.mnemonic == ZYDIS_MNEMONIC_MOV && instr.operand_count >= 2 &&
                    ops[0].type == ZYDIS_OPERAND_TYPE_MEMORY &&
                    ops[0].mem.base == ZYDIS_REGISTER_RIP &&
                    ops[0].element_size == 8 && // byte-sized
                    ops[1].type == ZYDIS_OPERAND_TYPE_REGISTER)
                {
                    uintptr_t target = preInitPre + offset + instr.length + (int64_t)ops[0].mem.disp.value;

                    if (target >= g_dataStart && target < g_dataStart + g_dataSize)
                    {
                        // Check if this and previous target are 1 byte apart
                        if (prevTarget && target == prevTarget + 1 &&
                            offset - prevOffset < 20)
                        {
                            Addr.GIsClient = (bool *)prevTarget;
                            Addr.GIsServer = (bool *)target;
                            Addr.GIsRunningCommandlet = (bool *)(prevTarget - 3);
                            Log("[Scanner] GIsClient = 0x%llX\n", (unsigned long long)prevTarget);
                            Log("[Scanner] GIsServer = 0x%llX\n", (unsigned long long)target);
                            Log("[Scanner] GIsRunningCommandlet = 0x%llX (GIsClient-3)\n", (unsigned long long)(prevTarget - 3));
                            break;
                        }
                        prevTarget = target;
                        prevOffset = offset;
                    }
                }
                offset += instr.length;
            }
            else
            {
                offset++;
            }
        }
    }

    // GIsFirstInstance: In MarinerEngine::Init at +0x20, CMP [rip+disp], reg
    Log("[Scanner] Resolving GIsFirstInstance from MarinerEngine::Init...\n");
    {
        uintptr_t target = FindRipRelativeTarget(engineInit, 0x40, ZYDIS_MNEMONIC_CMP, 0);
        if (target)
        {
            Addr.GIsFirstInstance = (bool *)target;
            Log("[Scanner] GIsFirstInstance = 0x%llX\n", (unsigned long long)target);
        }
    }

    // GEngine: In FEngineLoop::Init, first MOV STORE [rip+disp], reg targeting .data (at ~+0x1F5)
    Log("[Scanner] Resolving GEngine from FEngineLoop::Init...\n");
    {
        uintptr_t target = FindRipRelativeTarget(engineLoopInit, 0x300, ZYDIS_MNEMONIC_MOV, 0, 0); // operandIdx=0 = destination (store)
        if (target)
        {
            Addr.GEngine = (void **)target;
            Log("[Scanner] GEngine = 0x%llX\n", (unsigned long long)target);
        }
    }

    // GMalloc: find the .data pointer most frequently referenced before FF 50 20 (Free vtable call)
    Log("[Scanner] Resolving GMalloc...\n");
    {
        struct
        {
            uintptr_t ptr;
            int count;
        } candidates[64] = {};
        int numCandidates = 0;

        const uint8_t *p = (const uint8_t *)g_textStart;
        const uint8_t *end = p + g_textSize - 25;

        while (p < end)
        {
            // Look for: 48 8B 0D [disp32] ... FF 50 20 within 25 bytes
            if (p[0] == 0x48 && p[1] == 0x8B && p[2] == 0x0D)
            {
                int32_t disp = *(int32_t *)(p + 3);
                uintptr_t target = (uintptr_t)(p + 7) + disp;

                if (target >= g_dataStart && target < g_dataStart + g_dataSize)
                {
                    for (int j = 7; j < 25 && p + j + 2 < end; j++)
                    {
                        if (p[j] == 0xFF && p[j + 1] == 0x50 && p[j + 2] == 0x20)
                        {
                            // Tally this candidate
                            bool found = false;
                            for (int c = 0; c < numCandidates; c++)
                            {
                                if (candidates[c].ptr == target)
                                {
                                    candidates[c].count++;
                                    found = true;
                                    break;
                                }
                            }
                            if (!found && numCandidates < 64)
                            {
                                candidates[numCandidates].ptr = target;
                                candidates[numCandidates].count = 1;
                                numCandidates++;
                            }
                            break;
                        }
                    }
                }
            }
            p++;
        }

        // Pick the one with the most hits
        int bestIdx = -1;
        int bestCount = 0;
        for (int c = 0; c < numCandidates; c++)
        {
            if (candidates[c].count > bestCount)
            {
                bestCount = candidates[c].count;
                bestIdx = c;
            }
        }

        if (bestIdx >= 0 && bestCount > 100) // GMalloc should have hundreds of refs
        {
            Addr.GMalloc = (void **)candidates[bestIdx].ptr;
            Log("[Scanner] GMalloc = 0x%llX (%d references)\n", (unsigned long long)candidates[bestIdx].ptr, bestCount);
        }
        else
        {
            Log("[Scanner] WARNING: GMalloc not found (best candidate had %d refs)\n", bestCount);
        }
    }

    // Validate all resolved
    if (!Addr.GIsClient || !Addr.GIsServer || !Addr.GIsFirstInstance || !Addr.GEngine || !Addr.GMalloc)
    {
        Log("[Scanner] ERROR: Failed to resolve one or more globals!\n");
        return false;
    }

    Log("[Scanner] All addresses resolved successfully\n");
    return true;
}

// ============================================================================
// UE4 memory allocation (via GMalloc vtable)
// ============================================================================
static void *UE4Malloc(size_t size)
{
    void *allocator = *Addr.GMalloc;
    if (!allocator)
    {
        Log("[UE4Malloc] ERROR: GMalloc is null!\n");
        return nullptr;
    }
    void **vtable = *(void ***)allocator;
    if (!vtable)
    {
        Log("[UE4Malloc] ERROR: GMalloc vtable is null!\n");
        return nullptr;
    }
    typedef void *(__fastcall * MallocFn)(void *, size_t, uint32_t);
    auto fn = (MallocFn)vtable[0x10 / 8];
    if (!fn)
    {
        Log("[UE4Malloc] ERROR: Malloc vtable entry is null!\n");
        return nullptr;
    }
    return fn(allocator, size, 0);
}

static void UE4Free(void *ptr)
{
    if (!ptr)
        return;
    void *allocator = *Addr.GMalloc;
    if (!allocator)
        return;
    void **vtable = *(void ***)allocator;
    if (!vtable)
        return;
    typedef void(__fastcall * FreeFn)(void *, void *);
    auto fn = (FreeFn)vtable[0x20 / 8];
    if (!fn)
        return;
    fn(allocator, ptr);
}

// ============================================================================
// FString / FMarinerServerTravelConfig
// ============================================================================
struct FString
{
    wchar_t *Data;
    int32_t ArrayNum;
    int32_t ArrayMax;

    FString() : Data(nullptr), ArrayNum(0), ArrayMax(0) {}
    void Set(const wchar_t *str)
    {
        int len = (int)wcslen(str) + 1;
        Data = (wchar_t *)UE4Malloc(len * sizeof(wchar_t));
        wcscpy_s(Data, len, str);
        ArrayNum = len;
        ArrayMax = len;
    }
    void Free()
    {
        if (Data)
        {
            UE4Free(Data);
            Data = nullptr;
        }
        ArrayNum = 0;
        ArrayMax = 0;
    }
};

struct __declspec(align(8)) FMarinerServerTravelConfig
{
    FString MapName;              // 0x00
    FString GameModeName;         // 0x10
    int32_t NumBots;              // 0x20
    int32_t BotDifficulty;        // 0x24
    bool bAllowJoining;           // 0x28
    bool bLoadTransition;         // 0x29
    void *MovieToPlay;            // 0x30
    bool bCanSkipMovieOnceLoaded; // 0x38
};
static_assert(sizeof(FMarinerServerTravelConfig) == 0x40, "FMarinerServerTravelConfig size mismatch");
static_assert(offsetof(FMarinerServerTravelConfig, MapName) == 0x00, "MapName offset");
static_assert(offsetof(FMarinerServerTravelConfig, GameModeName) == 0x10, "GameModeName offset");
static_assert(offsetof(FMarinerServerTravelConfig, NumBots) == 0x20, "NumBots offset");
static_assert(offsetof(FMarinerServerTravelConfig, BotDifficulty) == 0x24, "BotDifficulty offset");
static_assert(offsetof(FMarinerServerTravelConfig, bAllowJoining) == 0x28, "bAllowJoining offset");
static_assert(offsetof(FMarinerServerTravelConfig, bLoadTransition) == 0x29, "bLoadTransition offset");
static_assert(offsetof(FMarinerServerTravelConfig, MovieToPlay) == 0x30, "MovieToPlay offset");
static_assert(offsetof(FMarinerServerTravelConfig, bCanSkipMovieOnceLoaded) == 0x38, "bCanSkipMovieOnceLoaded offset");

// ============================================================================
// FConfigCacheIni::GetString hook - override config values without file patching
// ============================================================================
typedef bool(__fastcall *ConfigGetString_fn)(void *thisPtr, const wchar_t *section, const wchar_t *key, FString *value, const FString *filename);
static ConfigGetString_fn Original_ConfigGetString = nullptr;

// ============================================================================
// INI-driven config overrides
// ============================================================================
// Key format: "Section\0Key" -> value
static std::unordered_map<std::wstring, std::wstring> g_iniOverrides;

static std::string GetGameRootPath()
{
    char exePath[MAX_PATH];
    GetModuleFileNameA(nullptr, exePath, MAX_PATH);
    std::string exeDir(exePath);
    size_t binPos = exeDir.rfind("\\Binaries\\");
    if (binPos == std::string::npos)
        binPos = exeDir.rfind("/Binaries/");
    if (binPos != std::string::npos)
        return exeDir.substr(0, binPos) + "\\..\\";
    return "";
}

static void LoadIniFile(const std::string &iniPath)
{

    FILE *f = nullptr;
    fopen_s(&f, iniPath.c_str(), "r");
    if (!f)
    {
        Log("[Config] No Overrides.ini found at %s\n", iniPath.c_str());
        return;
    }

    Log("[Config] Loading overrides from %s\n", iniPath.c_str());

    char line[2048];
    std::wstring currentSection;
    int count = 0;

    while (fgets(line, sizeof(line), f))
    {
        // Trim trailing newline/whitespace
        char *end = line + strlen(line) - 1;
        while (end >= line && (*end == '\n' || *end == '\r' || *end == ' '))
            *end-- = 0;

        // Skip empty lines and comments
        char *p = line;
        while (*p == ' ' || *p == '\t')
            p++;
        if (*p == 0 || *p == ';' || *p == '#')
            continue;

        // Section header
        if (*p == '[')
        {
            char *close = strchr(p, ']');
            if (close)
            {
                *close = 0;
                int wlen = MultiByteToWideChar(CP_UTF8, 0, p + 1, -1, nullptr, 0);
                currentSection.resize(wlen - 1);
                MultiByteToWideChar(CP_UTF8, 0, p + 1, -1, &currentSection[0], wlen);
            }
            continue;
        }

        // Key=Value
        char *eq = strchr(p, '=');
        if (!eq || currentSection.empty())
            continue;

        *eq = 0;
        const char *keyStr = p;
        const char *valStr = eq + 1;

        // Trim key
        char *keyEnd = eq - 1;
        while (keyEnd >= p && (*keyEnd == ' ' || *keyEnd == '\t'))
            *keyEnd-- = 0;

        // Trim value leading whitespace
        while (*valStr == ' ' || *valStr == '\t')
            valStr++;

        // Strip surrounding quotes from value
        size_t valLen = strlen(valStr);
        if (valLen >= 2 && valStr[0] == '"' && valStr[valLen - 1] == '"')
        {
            valStr++;
            valLen -= 2;
        }

        // Convert to wide
        int wKeyLen = MultiByteToWideChar(CP_UTF8, 0, keyStr, -1, nullptr, 0);
        std::wstring wKey(wKeyLen - 1, 0);
        MultiByteToWideChar(CP_UTF8, 0, keyStr, -1, &wKey[0], wKeyLen);

        int wValLen = MultiByteToWideChar(CP_UTF8, 0, valStr, (int)valLen, nullptr, 0);
        std::wstring wVal(wValLen, 0);
        MultiByteToWideChar(CP_UTF8, 0, valStr, (int)valLen, &wVal[0], wValLen);

        // Store as "Section\0Key" -> value
        std::wstring lookupKey = currentSection + L'\0' + wKey;
        g_iniOverrides[lookupKey] = wVal;
        count++;
    }

    fclose(f);
    Log("[Config] Loaded %d overrides from INI\n", count);
}

static const std::wstring *FindIniOverride(const wchar_t *section, const wchar_t *key)
{
    std::wstring lookupKey = std::wstring(section) + L'\0' + key;
    auto it = g_iniOverrides.find(lookupKey);
    if (it != g_iniOverrides.end())
        return &it->second;
    return nullptr;
}

static void SetFString(FString *str, const wchar_t *newVal)
{
    int newLen = (int)wcslen(newVal) + 1;
    if (str->Data && str->ArrayMax >= newLen)
    {
        wcscpy_s(str->Data, str->ArrayMax, newVal);
        str->ArrayNum = newLen;
    }
    else
    {
        wchar_t *buf = (wchar_t *)UE4Malloc(newLen * sizeof(wchar_t));
        if (buf)
        {
            wcscpy_s(buf, newLen, newVal);
            if (str->Data)
                UE4Free(str->Data);
            str->Data = buf;
            str->ArrayNum = newLen;
            str->ArrayMax = newLen;
        }
    }
}

static bool __fastcall Hooked_ConfigGetString(void *thisPtr, const wchar_t *section, const wchar_t *key, FString *value, const FString *filename)
{
    bool result = Original_ConfigGetString(thisPtr, section, key, value, filename);

    if (!section || !key)
        return result;

    const std::wstring *override = FindIniOverride(section, key);
    if (override)
    {
        Log("[Config] %ls.%ls = '%ls' -> '%ls'\n", section, key, (value->Data ? value->Data : L"(null)"), override->c_str());
        SetFString(value, override->c_str());
        return true;
    }

    return result;
}

// ============================================================================
// FConfigCacheIni::GetInt hook
// ============================================================================
typedef bool(__fastcall *ConfigGetInt_fn)(void *thisPtr, const wchar_t *section, const wchar_t *key, int *value, const FString *filename);
static ConfigGetInt_fn Original_ConfigGetInt = nullptr;

static bool __fastcall Hooked_ConfigGetInt(void *thisPtr, const wchar_t *section, const wchar_t *key, int *value, const FString *filename)
{
    bool result = Original_ConfigGetInt(thisPtr, section, key, value, filename);

    if (!section || !key)
        return result;

    const std::wstring *override = FindIniOverride(section, key);
    if (override)
    {
        int newVal = _wtoi(override->c_str());
        Log("[Config] %ls.%ls = %d -> %d\n", section, key, value ? *value : 0, newVal);
        if (value)
            *value = newVal;
        return true;
    }

    return result;
}

// ============================================================================
// State & command line
// ============================================================================
static std::string g_sessionDataJson;
static std::string g_matchmakerDataJson;
static void *g_dsmPtr = nullptr;

static void PatchGlobals()
{
    Log("[MarinerServer] Before: GIsClient=%d, GIsServer=%d\n", *Addr.GIsClient, *Addr.GIsServer);
    *Addr.GIsClient = false;
    *Addr.GIsServer = true;
    *Addr.GIsRunningCommandlet = false;
    *Addr.GIsFirstInstance = true;
    Log("[MarinerServer] After: GIsClient=%d, GIsServer=%d, GIsFirstInstance=%d\n", *Addr.GIsClient, *Addr.GIsServer, *Addr.GIsFirstInstance);
}

static void PatchIsDedicatedServerInstance()
{
    uint8_t *func = (uint8_t *)Addr.IsDedicatedServerInstance;
    DWORD oldProtect;
    VirtualProtect(func, 3, PAGE_EXECUTE_READWRITE, &oldProtect);
    func[0] = 0xB0;
    func[1] = 0x01;
    func[2] = 0xC3;
    VirtualProtect(func, 3, oldProtect, &oldProtect);
    FlushInstructionCache(GetCurrentProcess(), func, 3);
    Log("[MarinerServer] Patched IsDedicatedServerInstance -> return true\n");
}

// ============================================================================
// Base64 decoder
// ============================================================================
static std::string Base64Decode(const std::string &input)
{
    static const int lookup[] = {
        -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
        -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 62, -1, -1, -1, 63, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, -1, -1, -1, -1, -1, -1,
        -1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, -1, -1, -1, -1, -1,
        -1, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, -1, -1, -1, -1, -1};

    std::string out;
    int val = 0, bits = -8;
    for (unsigned char c : input)
    {
        if (c == '=' || c >= 128)
            break;
        int v = lookup[c];
        if (v < 0)
            continue;
        val = (val << 6) | v;
        bits += 6;
        if (bits >= 0)
        {
            out.push_back((char)((val >> bits) & 0xFF));
            bits -= 8;
        }
    }
    return out;
}

static void ParseCommandLine()
{
    const char *cmdLine = GetCommandLineA();
    const char *flag;

    // Parse -sessiondata (base64-encoded JSON for FMangoGameSessionData)
    flag = strstr(cmdLine, "-sessiondata=");
    if (!flag)
        flag = strstr(cmdLine, "-sessiondata ");
    if (flag)
    {
        const char *s = flag + 13;
        if (*s == '"')
        {
            s++;
            const char *e = strchr(s, '"');
            g_sessionDataJson.assign(s, e ? e : s + strlen(s));
        }
        else
        {
            const char *e = s;
            while (*e && *e != ' ' && *e != '\t')
                e++;
            g_sessionDataJson.assign(s, e);
        }
        if (!g_sessionDataJson.empty())
        {
            g_sessionDataJson = Base64Decode(g_sessionDataJson);
            Log("[MarinerServer] Parsed -sessiondata (%zu bytes): %s\n", g_sessionDataJson.size(), g_sessionDataJson.c_str());
        }
    }

    // Parse -matchmakerdata "json" (used for servers spawned through matchmaking, not included for private matches)
    flag = strstr(cmdLine, "-matchmakerdata=");
    if (!flag)
        flag = strstr(cmdLine, "-matchmakerdata ");
    if (flag)
    {
        const char *s = flag + 16;
        if (*s == '"')
        {
            s++;
            const char *e = strchr(s, '"');
            g_matchmakerDataJson.assign(s, e ? e : s + strlen(s));
        }
        else
        {
            const char *e = s;
            while (*e && *e != ' ' && *e != '\t')
                e++;
            g_matchmakerDataJson.assign(s, e);
        }
        if (!g_matchmakerDataJson.empty())
        {
            g_matchmakerDataJson = Base64Decode(g_matchmakerDataJson);
            Log("[MarinerServer] Parsed -matchmakerdata (%zu bytes): %s\n", g_matchmakerDataJson.size(), g_matchmakerDataJson.c_str());
        }
    }
}

// ============================================================================
// Parse UE4 FGuid from string "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
// ============================================================================
static bool ParseFGuid(const char *str, uint32_t &a, uint32_t &b, uint32_t &c, uint32_t &d)
{
    // Format: AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE
    // UE4 mapping: A=AAAAAAAA, B=BBBBCCCC, C=DDDDEEEE, D=EEEEEEEE
    uint32_t parts[5];
    if (sscanf_s(str, "%8x-%4x-%4x-%4x-%4x%8x", &parts[0], &parts[1], &parts[2], &parts[3], &parts[4], &d) != 6)
        return false;
    a = parts[0];
    b = (parts[1] << 16) | parts[2];
    c = (parts[3] << 16) | parts[4];
    return true;
}

// ============================================================================
// Game session population
// ============================================================================

// FMangoGameSession layout within UMangoDedicatedServerManager:
//   DSM+0x40: FGuid PlaylistGuid
//   DSM+0x50: FMangoGameSession MangoGameSession
//     +0x00 (DSM+0x50): FString MatchmakerDataString
//     +0x10 (DSM+0x60): FString GameSessionDataString
//     +0x20 (DSM+0x70): FString GameSessionId
//     +0x30 (DSM+0x80): FString Name
//     +0x40 (DSM+0x90): FString FleetId
//     +0x50 (DSM+0xA0): FString DnsName
//     +0x60 (DSM+0xB0): FString IpAddress
//     +0x70 (DSM+0xC0): int Port
//     +0x74 (DSM+0xC4): int MaximumPlayerSessionCount
//     +0x78 (DSM+0xC8): FMangoGameSessionData GameSessionData (0x70 bytes, has vtable)
//     +0xE8 (DSM+0x138): TOptional<FMangoMatchmakerData> MatchmakerData (0x40 bytes)

// Matchmaker team/player data for direct memory population
struct MatchmakerPlayer
{
    std::string playerId;
};

struct MatchmakerTeam
{
    std::string name;
    std::vector<MatchmakerPlayer> players;
};

struct GameSessionParams
{
    // Session identity
    const char *sessionId;
    const char *name;
    const char *fleetId;
    const char *dnsName;
    const char *ipAddress;
    int port;
    int maxPlayerSessionCount;

    // GameSessionData JSON (parsed via FromJson - has valid vtable)
    const char *gameSessionDataJson;

    // Matchmaker data (all methods stripped from client build, so we populate directly in memory)
    std::string matchId;
    std::string matchmakingConfigurationArn;
    std::vector<MatchmakerTeam> teams;
};

static void SetFStringAt(void *base, size_t offset, const wchar_t *value)
{
    FString *str = (FString *)((char *)base + offset);
    SetFString(str, value);
}

static void SetFStringAtFromUtf8(void *base, size_t offset, const char *utf8)
{
    if (!utf8 || !utf8[0])
        return;
    int wlen = MultiByteToWideChar(CP_UTF8, 0, utf8, -1, nullptr, 0);
    std::wstring wstr(wlen, 0);
    MultiByteToWideChar(CP_UTF8, 0, utf8, -1, &wstr[0], wlen);
    SetFStringAt(base, offset, wstr.c_str());
}

static bool PopulateGameSession(void *dsmPtr, const GameSessionParams &params)
{
    Log("[Session] Populating game session on DSM 0x%llX\n", (unsigned long long)(uintptr_t)dsmPtr);

    // PlaylistGuid at DSM+0x40 is set by SelectAndLoadMap from the matched playlist

    // FMangoGameSession is at DSM+0x50
    char *session = (char *)dsmPtr + 0x50;

    // Set FString fields
    SetFStringAtFromUtf8(session, 0x20, params.sessionId);
    SetFStringAtFromUtf8(session, 0x30, params.name);
    SetFStringAtFromUtf8(session, 0x40, params.fleetId);
    SetFStringAtFromUtf8(session, 0x50, params.dnsName);
    SetFStringAtFromUtf8(session, 0x60, params.ipAddress);

    // Set ints
    *(int *)(session + 0x70) = params.port;
    *(int *)(session + 0x74) = params.maxPlayerSessionCount;

    Log("[Session] SessionId=%s, Name=%s, Port=%d, MaxPlayers=%d\n", params.sessionId ? params.sessionId : "", params.name ? params.name : "", params.port, params.maxPlayerSessionCount);

    // Set GameSessionDataString and parse via FromJson
    if (params.gameSessionDataJson && params.gameSessionDataJson[0])
    {
        // Set the raw JSON string at +0x10 (GameSessionDataString)
        SetFStringAtFromUtf8(session, 0x10, params.gameSessionDataJson);

        // GameSessionData is at session+0x78, it has a vtable
        // vtable[0x30/8 = 6] = bool FromJson(const FString*)
        void *gameSessionData = session + 0x78;
        void **vtable = *(void ***)gameSessionData;

        if (vtable)
        {
            FString *dataStr = (FString *)(session + 0x10);
            typedef bool(__fastcall * FromJsonFn)(void *, const FString *);
            bool ok = ((FromJsonFn)vtable[6])(gameSessionData, dataStr);
            Log("[Session] GameSessionData.FromJson() = %d\n", ok);
        }
        else
        {
            Log("[Session] WARNING: GameSessionData vtable is null\n");
        }
    }

    // Populate MatchmakerData directly in memory (vtables are missing from client build)
    // Does not restore vtables or call any methods, but neither does the client code so this is sufficient for what we need
    // TOptional<FMangoMatchmakerData> is at session+0xE8 (0x38 value + bool bIsSet at +0x38)
    if (!params.teams.empty())
    {
        char *mmData = session + 0xE8;
        bool *bIsSet = (bool *)(mmData + 0x38);

        // Zero-init the TOptional value area
        memset(mmData, 0, 0x38);

        // FMangoMatchmakerData layout (0x38):
        //   +0x00: vtable
        //   +0x08: FString MatchId
        //   +0x18: FString MatchmakingConfigurationArn
        //   +0x28: TArray<FMangoMatchmakerTeam> Teams {Data*, ArrayNum, ArrayMax}

        SetFStringAtFromUtf8(mmData, 0x08, params.matchId.c_str());
        SetFStringAtFromUtf8(mmData, 0x18, params.matchmakingConfigurationArn.c_str());

        // Allocate Teams array
        int numTeams = (int)params.teams.size();
        size_t teamStructSize = 0x28; // sizeof(FMangoMatchmakerTeam)
        char *teamsArray = (char *)UE4Malloc(numTeams * teamStructSize);
        memset(teamsArray, 0, numTeams * teamStructSize);

        for (int t = 0; t < numTeams; t++)
        {
            char *team = teamsArray + t * teamStructSize;
            const auto &srcTeam = params.teams[t];

            // FMangoMatchmakerTeam layout (0x28):
            //   +0x00: vtable
            //   +0x08: FString Name
            //   +0x18: TArray<FMangoMatchmakerPlayer> Players

            SetFStringAtFromUtf8(team, 0x08, srcTeam.name.c_str());

            // Allocate Players array
            int numPlayers = (int)srcTeam.players.size();
            size_t playerStructSize = 0x18; // sizeof(FMangoMatchmakerPlayer)
            char *playersArray = (char *)UE4Malloc(numPlayers * playerStructSize);
            memset(playersArray, 0, numPlayers * playerStructSize);

            for (int p = 0; p < numPlayers; p++)
            {
                char *player = playersArray + p * playerStructSize;
                // FMangoMatchmakerPlayer layout (0x18):
                //   +0x00: vtable
                //   +0x08: FString PlayerId
                SetFStringAtFromUtf8(player, 0x08, srcTeam.players[p].playerId.c_str());

                Log("[Session]   Team[%d].Player[%d] = %s\n", t, p, srcTeam.players[p].playerId.c_str());
            }

            // Set Players TArray: {Data*, ArrayNum, ArrayMax}
            *(char **)(team + 0x18) = playersArray;
            *(int *)(team + 0x20) = numPlayers;
            *(int *)(team + 0x24) = numPlayers;

            Log("[Session]   Team[%d] = '%s' (%d players)\n", t, srcTeam.name.c_str(), numPlayers);
        }

        // Set Teams TArray on MatchmakerData
        *(char **)(mmData + 0x28) = teamsArray;
        *(int *)(mmData + 0x30) = numTeams;
        *(int *)(mmData + 0x34) = numTeams;

        // Mark TOptional as set
        *bIsSet = true;

        Log("[Session] MatchmakerData populated: MatchId=%s, %d teams\n", params.matchId.c_str(), numTeams);
    }

    return true;
}

// ============================================================================
// Minimal JSON parser for matchmaker data
// ============================================================================
static std::string JsonGetString(const std::string &json, const std::string &key)
{
    std::string search = "\"" + key + "\"";
    size_t pos = json.find(search);
    if (pos == std::string::npos)
        return "";
    pos = json.find(':', pos + search.length());
    if (pos == std::string::npos)
        return "";
    pos = json.find('"', pos + 1);
    if (pos == std::string::npos)
        return "";
    pos++;
    size_t end = json.find('"', pos);
    if (end == std::string::npos)
        return "";
    return json.substr(pos, end - pos);
}

static bool JsonGetBool(const std::string &json, const std::string &key)
{
    std::string search = "\"" + key + "\"";
    size_t pos = json.find(search);
    if (pos == std::string::npos)
        return false;
    pos = json.find(':', pos + search.length());
    if (pos == std::string::npos)
        return false;
    pos++;
    while (pos < json.size() && (json[pos] == ' ' || json[pos] == '\t'))
        pos++;
    return json.compare(pos, 4, "true") == 0;
}

static std::string JsonGetArray(const std::string &json, const std::string &key)
{
    std::string search = "\"" + key + "\"";
    size_t pos = json.find(search);
    if (pos == std::string::npos)
        return "";
    pos = json.find('[', pos);
    if (pos == std::string::npos)
        return "";
    int depth = 0;
    size_t start = pos;
    for (size_t i = pos; i < json.size(); i++)
    {
        if (json[i] == '[')
            depth++;
        else if (json[i] == ']')
        {
            depth--;
            if (depth == 0)
                return json.substr(start, i - start + 1);
        }
    }
    return "";
}

static std::vector<std::string> JsonSplitArrayObjects(const std::string &arr)
{
    std::vector<std::string> result;
    int depth = 0;
    size_t objStart = 0;
    for (size_t i = 0; i < arr.size(); i++)
    {
        if (arr[i] == '{')
        {
            if (depth == 0)
                objStart = i;
            depth++;
        }
        else if (arr[i] == '}')
        {
            depth--;
            if (depth == 0)
                result.push_back(arr.substr(objStart, i - objStart + 1));
        }
    }
    return result;
}

static void ParseMatchmakerJson(const std::string &json, GameSessionParams &params)
{
    params.matchId = JsonGetString(json, "MatchId");
    params.matchmakingConfigurationArn = JsonGetString(json, "MatchmakingConfigurationArn");

    std::string teamsArr = JsonGetArray(json, "Teams");
    if (teamsArr.empty())
        return;

    auto teamObjects = JsonSplitArrayObjects(teamsArr);
    for (const auto &teamJson : teamObjects)
    {
        MatchmakerTeam team;
        team.name = JsonGetString(teamJson, "Name");

        std::string playersArr = JsonGetArray(teamJson, "Players");
        if (!playersArr.empty())
        {
            auto playerObjects = JsonSplitArrayObjects(playersArr);
            for (const auto &playerJson : playerObjects)
            {
                MatchmakerPlayer player;
                player.playerId = JsonGetString(playerJson, "PlayerId");
                if (!player.playerId.empty())
                    team.players.push_back(player);
            }
        }
        params.teams.push_back(team);
    }

    Log("[Session] Parsed matchmaker JSON: MatchId=%s, %zu teams\n", params.matchId.c_str(), params.teams.size());
}

// ============================================================================
// Hooks
// ============================================================================
typedef int32_t(__fastcall *PreInitPostStartupScreen_fn)(void *thisPtr, const wchar_t *cmdLine);
static PreInitPostStartupScreen_fn Original_PreInitPostStartupScreen = nullptr;

static int32_t __fastcall Hooked_PreInitPostStartupScreen(void *thisPtr, const wchar_t *cmdLine)
{
    Log("[MarinerServer] === PreInitPostStartupScreen called ===\n");
    PatchGlobals();
    PatchIsDedicatedServerInstance();

    int32_t result = Original_PreInitPostStartupScreen(thisPtr, cmdLine);

    Log("[MarinerServer] PreInitPostStartupScreen returned %d, re-verifying globals...\n", result);
    PatchGlobals();
    return result;
}

typedef void(__fastcall *DSM_OnConnectionStateChanged_fn)(void *thisPtr, int32_t state);
static DSM_OnConnectionStateChanged_fn Original_DSM_OnConnectionStateChanged = nullptr;
typedef void(__fastcall *ServerTravel_fn)(void *gameInstance, FMarinerServerTravelConfig *config);

static void __fastcall Hooked_DSM_OnConnectionStateChanged(void *thisPtr, int32_t state)
{
    Log("[DSM] OnConnectionStateChanged(this=0x%llX, state=%d)\n",
        (unsigned long long)(uintptr_t)thisPtr, state);

    // Capture DSM pointer for later use
    g_dsmPtr = thisPtr;

    if (state != 11)
        return;

    if (g_sessionDataJson.empty())
    {
        Log("[DSM] No -sessiondata specified\n");
        // TODO: RequestExit
        return;
    }

    // Populate game session data
    GameSessionParams params = {};
    params.sessionId = "local-session";
    params.name = "LocalMatch";
    params.fleetId = "local-fleet";
    params.dnsName = "localhost";
    params.ipAddress = "127.0.0.1";
    params.port = 7777;
    params.maxPlayerSessionCount = 6;
    params.gameSessionDataJson = g_sessionDataJson.c_str();

    if (!g_matchmakerDataJson.empty())
        ParseMatchmakerJson(g_matchmakerDataJson, params);

    PopulateGameSession(thisPtr, params);

    // Calling AddGameSessionMangoIds triggers data fetch from the server, after all data is populated DSM sets up and loads the map.
    {
        // Get PlayerManager: GEngine+0xDF0 = GameInstance, GameInstance+0x230 = UMangoManagers, managers+0xA8 = PlayerManager
        void *engine = *Addr.GEngine;
        if (!engine)
        {
            Log("[DSM] ERROR: GEngine null\n");
            return;
        }
        void *gameInstance = *(void **)((char *)engine + 0xDF0);
        if (!gameInstance)
        {
            Log("[DSM] ERROR: GameInstance null\n");
            return;
        }
        void *managers = *(void **)((char *)gameInstance + 0x230);
        if (!managers)
        {
            Log("[DSM] ERROR: UMangoManagers null\n");
            return;
        }
        void *playerManager = *(void **)((char *)managers + 0xA8);
        if (!playerManager)
        {
            Log("[DSM] ERROR: PlayerManager null\n");
            return;
        }

        Log("[DSM] PlayerManager = 0x%llX\n", (unsigned long long)(uintptr_t)playerManager);

        // Collect player IDs based on session type
        // FMangoId is { FString MangoIdStr } = 0x10 bytes
        std::vector<std::string> allPlayerIds;

        bool isMatchmaking = JsonGetBool(g_sessionDataJson, "bIsMatchmakingSession");
        bool isPrivateMatch = JsonGetBool(g_sessionDataJson, "bIsPrivateMatchSession");

        if (isMatchmaking)
        {
            // Matchmade: player IDs come from matchmaker teams
            for (const auto &team : params.teams)
                for (const auto &player : team.players)
                    if (!player.playerId.empty())
                        allPlayerIds.push_back(player.playerId);
            Log("[DSM] Matchmaking session: collected %zu player IDs from matchmaker data\n", allPlayerIds.size());
        }
        else if (isPrivateMatch)
        {
            // Private match: player IDs come from session data Team1/Team2
            for (const char *teamKey : {"Team1", "Team2"})
            {
                std::string teamArr = JsonGetArray(g_sessionDataJson, teamKey);
                if (teamArr.empty())
                    continue;
                auto players = JsonSplitArrayObjects(teamArr);
                for (const auto &pJson : players)
                {
                    std::string pid = JsonGetString(pJson, "PlayerId");
                    if (!pid.empty())
                        allPlayerIds.push_back(pid);
                }
            }
            Log("[DSM] Private match: collected %zu player IDs from session data\n", allPlayerIds.size());
        }
        else
        {
            Log("[DSM] ERROR: Session is neither matchmaking nor private match!\n");
            // TODO: RequestExit
            return;
        }

        if (allPlayerIds.empty())
        {
            Log("[DSM] ERROR: No player IDs found in session data!\n");
            // TODO: RequestExit
            return;
        }

        int numPlayers = (int)allPlayerIds.size();
        size_t mangoIdSize = 0x10; // sizeof(FMangoId) = sizeof(FString)
        char *mangoIdArray = (char *)UE4Malloc(numPlayers * mangoIdSize);
        memset(mangoIdArray, 0, numPlayers * mangoIdSize);

        for (int i = 0; i < numPlayers; i++)
        {
            SetFStringAtFromUtf8(mangoIdArray, i * mangoIdSize, allPlayerIds[i].c_str());
            Log("[DSM] Adding MangoId[%d] = %s\n", i, allPlayerIds[i].c_str());
        }

        // Build TArray struct on stack: { Data*, ArrayNum, ArrayMax }
        struct
        {
            char *Data;
            int ArrayNum;
            int ArrayMax;
        } mangoIds;
        mangoIds.Data = mangoIdArray;
        mangoIds.ArrayNum = numPlayers;
        mangoIds.ArrayMax = numPlayers;

        // Call AddGameSessionMangoIds(playerManager, &mangoIds)
        typedef void(__fastcall * AddGameSessionMangoIds_fn)(void *, const void *);
        auto fn = (AddGameSessionMangoIds_fn)Addr.AddGameSessionMangoIds;
        Log("[DSM] Calling AddGameSessionMangoIds with %d players\n", numPlayers);
        fn(playerManager, &mangoIds);
        Log("[DSM] AddGameSessionMangoIds complete\n");
    }
}

// ============================================================================
// Install hooks
// ============================================================================
static bool InstallHooks()
{
    Original_PreInitPostStartupScreen = (PreInitPostStartupScreen_fn)Addr.PreInitPostStartupScreen;
    Original_DSM_OnConnectionStateChanged = (DSM_OnConnectionStateChanged_fn)Addr.OnConnectionStateChanged;
    Original_ConfigGetString = (ConfigGetString_fn)Addr.ConfigGetString;
    Original_ConfigGetInt = (ConfigGetInt_fn)Addr.ConfigGetInt;

    LONG error = DetourTransactionBegin();
    if (error != NO_ERROR)
    {
        Log("[MarinerServer] DetourTransactionBegin failed: %ld\n", error);
        return false;
    }

    error = DetourUpdateThread(GetCurrentThread());
    if (error != NO_ERROR)
    {
        Log("[MarinerServer] DetourUpdateThread failed: %ld\n", error);
        return false;
    }

    error = DetourAttach((PVOID *)&Original_PreInitPostStartupScreen, (PVOID)Hooked_PreInitPostStartupScreen);
    if (error != NO_ERROR)
    {
        Log("[MarinerServer] DetourAttach PreInitPostStartupScreen failed: %ld\n", error);
        return false;
    }

    error = DetourAttach((PVOID *)&Original_DSM_OnConnectionStateChanged, (PVOID)Hooked_DSM_OnConnectionStateChanged);
    if (error != NO_ERROR)
    {
        Log("[MarinerServer] DetourAttach OnConnectionStateChanged failed: %ld\n", error);
        return false;
    }

    error = DetourAttach((PVOID *)&Original_ConfigGetString, (PVOID)Hooked_ConfigGetString);
    if (error != NO_ERROR)
    {
        Log("[MarinerServer] DetourAttach ConfigGetString failed: %ld\n", error);
        return false;
    }

    error = DetourAttach((PVOID *)&Original_ConfigGetInt, (PVOID)Hooked_ConfigGetInt);
    if (error != NO_ERROR)
    {
        Log("[MarinerServer] DetourAttach ConfigGetInt failed: %ld\n", error);
        return false;
    }

    error = DetourTransactionCommit();
    if (error != NO_ERROR)
    {
        Log("[MarinerServer] DetourTransactionCommit failed: %ld\n", error);
        return false;
    }

    Log("[MarinerServer] All hooks installed\n");
    return true;
}

// ============================================================================
// DLL Entry Point
// ============================================================================
BOOL APIENTRY DllMain(HMODULE hModule, DWORD reason, LPVOID reserved)
{
    if (reason != DLL_PROCESS_ATTACH)
        return TRUE;

    DisableThreadLibraryCalls(hModule);
    g_imageBase = (uintptr_t)GetModuleHandleA(nullptr);

    InitLogging();
    Log("[MarinerServer] DLL loaded! Image base: 0x%llX\n", (unsigned long long)g_imageBase);

    InitSections();
    Log("[MarinerServer] .text: 0x%llX (0x%llX bytes), .data: 0x%llX (0x%llX bytes)\n", (unsigned long long)g_textStart, (unsigned long long)g_textSize, (unsigned long long)g_dataStart, (unsigned long long)g_dataSize);

    if (!ResolveAddresses())
    {
        Log("[MarinerServer] ERROR: Address resolution failed!\n");
        return TRUE;
    }

    std::string root = GetGameRootPath();
    LoadIniFile(root + "Overrides.ini");
    LoadIniFile(root + "ServerOverrides.ini");
    ParseCommandLine();

    if (!InstallHooks())
    {
        Log("[MarinerServer] ERROR: Failed to install hooks!\n");
        return TRUE;
    }

    Log("[MarinerServer] Initialization complete\n");
    return TRUE;
}
