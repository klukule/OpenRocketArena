#include <windows.h>
#include <detours/detours.h>
#include <Zydis/Zydis.h>
#include <cstdint>
#include <cstdio>
#include <cstdarg>
#include <cstring>
#include <string>
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

    if (g_logFile)
    {
        fprintf(g_logFile, "%s", buf);
        fflush(g_logFile);
    }
}

static void InitLogging()
{
    char exePath[MAX_PATH];
    GetModuleFileNameA(nullptr, exePath, MAX_PATH);
    char *lastSlash = strrchr(exePath, '\\');
    if (lastSlash)
        *(lastSlash + 1) = 0;
    strcat_s(exePath, "MarinerClient.log");
    fopen_s(&g_logFile, exePath, "w");
}

// ============================================================================
// Pattern scanner
// ============================================================================
static uintptr_t g_imageBase = 0;
static uintptr_t g_textStart = 0;
static size_t g_textSize = 0;

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

// ============================================================================
// Resolved addresses
// ============================================================================
static uintptr_t g_dataStart = 0;
static size_t g_dataSize = 0;

static void InitDataSection()
{
    auto *dos = (IMAGE_DOS_HEADER *)g_imageBase;
    auto *nt = (IMAGE_NT_HEADERS64 *)(g_imageBase + dos->e_lfanew);
    auto *section = IMAGE_FIRST_SECTION(nt);
    for (WORD i = 0; i < nt->FileHeader.NumberOfSections; i++, section++)
    {
        if (memcmp(section->Name, ".data", 5) == 0)
        {
            g_dataStart = g_imageBase + section->VirtualAddress;
            g_dataSize = section->Misc.VirtualSize;
        }
    }
}

static struct
{
    uintptr_t ConfigGetString;
    uintptr_t ConfigGetInt;
    void **GMalloc;
} Addr = {};

static bool ResolveAddresses()
{
    // FConfigCacheIni::GetString
    static const uint8_t pat_ConfigGetString[] = {
        0x4C, 0x89, 0x4C, 0x24, 0x20, 0x4C, 0x89, 0x44, 0x24, 0x18, 0x53, 0x56,
        0x57, 0x41, 0x55, 0x41, 0x56, 0x41, 0x57, 0x48, 0x83, 0xEC, 0x38, 0x4C};
    Addr.ConfigGetString = FindPattern(pat_ConfigGetString, sizeof(pat_ConfigGetString), "FConfigCacheIni::GetString");

    // FConfigCacheIni::GetInt (with wildcard on CALL displacement)
    static const uint8_t pat_ConfigGetInt[] = {
        0x40, 0x53, 0x48, 0x83, 0xEC, 0x40, 0x33, 0xC0, 0x49, 0x8B, 0xD9, 0x48, 0x89, 0x44, 0x24, 0x30,
        0x4C, 0x8D, 0x4C, 0x24, 0x30, 0x48, 0x89, 0x44, 0x24, 0x38, 0x48, 0x8B, 0x44, 0x24, 0x70, 0x48,
        0x89, 0x44, 0x24, 0x20, 0xE8, 0x00, 0x00, 0x00, 0x00, 0x84, 0xC0, 0x74, 0x1E, 0x83, 0x7C, 0x24,
        0x38, 0x00};
    static const uint8_t msk_ConfigGetInt[] = {
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1,
        1, 1};
    Addr.ConfigGetInt = FindPatternMasked(pat_ConfigGetInt, msk_ConfigGetInt, sizeof(pat_ConfigGetInt), "FConfigCacheIni::GetInt");

    if (!Addr.ConfigGetString || !Addr.ConfigGetInt)
    {
        Log("[Scanner] ERROR: Required patterns not found!\n");
        return false;
    }

    // GMalloc: find .data ptr most referenced before FF 50 20 (Free vtable call)
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

        int bestIdx = -1, bestCount = 0;
        for (int c = 0; c < numCandidates; c++)
        {
            if (candidates[c].count > bestCount)
            {
                bestCount = candidates[c].count;
                bestIdx = c;
            }
        }

        if (bestIdx >= 0 && bestCount > 100)
        {
            Addr.GMalloc = (void **)candidates[bestIdx].ptr;
            Log("[Scanner] GMalloc = 0x%llX (%d references)\n", (unsigned long long)candidates[bestIdx].ptr, bestCount);
        }
        else
        {
            Log("[Scanner] ERROR: GMalloc not found\n");
            return false;
        }
    }

    Log("[Scanner] All addresses resolved\n");
    return true;
}

// ============================================================================
// UE4 memory allocation
// ============================================================================
static void *UE4Malloc(size_t size)
{
    void *allocator = *Addr.GMalloc;
    if (!allocator)
        return nullptr;
    void **vtable = *(void ***)allocator;
    typedef void *(__fastcall * MallocFn)(void *, size_t, uint32_t);
    return ((MallocFn)vtable[0x10 / 8])(allocator, size, 0);
}

static void UE4Free(void *ptr)
{
    if (!ptr)
        return;
    void *allocator = *Addr.GMalloc;
    if (!allocator)
        return;
    void **vtable = *(void ***)allocator;
    typedef void(__fastcall * FreeFn)(void *, void *);
    ((FreeFn)vtable[0x20 / 8])(allocator, ptr);
}

// ============================================================================
// FString
// ============================================================================
struct FString
{
    wchar_t *Data;
    int32_t ArrayNum;
    int32_t ArrayMax;
};

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

// ============================================================================
// INI-driven config overrides
// ============================================================================
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
        char *end = line + strlen(line) - 1;
        while (end >= line && (*end == '\n' || *end == '\r' || *end == ' '))
            *end-- = 0;

        char *p = line;
        while (*p == ' ' || *p == '\t')
            p++;
        if (*p == 0 || *p == ';' || *p == '#')
            continue;

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

        char *eq = strchr(p, '=');
        if (!eq || currentSection.empty())
            continue;

        *eq = 0;
        const char *keyStr = p;
        const char *valStr = eq + 1;

        char *keyEnd = eq - 1;
        while (keyEnd >= p && (*keyEnd == ' ' || *keyEnd == '\t'))
            *keyEnd-- = 0;

        while (*valStr == ' ' || *valStr == '\t')
            valStr++;

        size_t valLen = strlen(valStr);
        if (valLen >= 2 && valStr[0] == '"' && valStr[valLen - 1] == '"')
        {
            valStr++;
            valLen -= 2;
        }

        int wKeyLen = MultiByteToWideChar(CP_UTF8, 0, keyStr, -1, nullptr, 0);
        std::wstring wKey(wKeyLen - 1, 0);
        MultiByteToWideChar(CP_UTF8, 0, keyStr, -1, &wKey[0], wKeyLen);

        int wValLen = MultiByteToWideChar(CP_UTF8, 0, valStr, (int)valLen, nullptr, 0);
        std::wstring wVal(wValLen, 0);
        MultiByteToWideChar(CP_UTF8, 0, valStr, (int)valLen, &wVal[0], wValLen);

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

// ============================================================================
// GetString hook
// ============================================================================
typedef bool(__fastcall *ConfigGetString_fn)(void *thisPtr, const wchar_t *section, const wchar_t *key, FString *value, const FString *filename);
static ConfigGetString_fn Original_ConfigGetString = nullptr;

static bool __fastcall Hooked_ConfigGetString(void *thisPtr, const wchar_t *section, const wchar_t *key, FString *value, const FString *filename)
{
    bool result = Original_ConfigGetString(thisPtr, section, key, value, filename);

    if (!section || !key)
        return result;

    const std::wstring *override = FindIniOverride(section, key);
    if (override)
    {
        Log("[Config] %ls.%ls -> '%ls'\n", section, key, override->c_str());
        SetFString(value, override->c_str());
        return true;
    }

    return result;
}

// ============================================================================
// GetInt hook
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
        Log("[Config] %ls.%ls -> %d\n", section, key, newVal);
        if (value)
            *value = newVal;
        return true;
    }

    return result;
}

// ============================================================================
// Install hooks
// ============================================================================
static bool InstallHooks()
{
    Original_ConfigGetString = (ConfigGetString_fn)Addr.ConfigGetString;
    Original_ConfigGetInt = (ConfigGetInt_fn)Addr.ConfigGetInt;

    LONG error = DetourTransactionBegin();
    if (error != NO_ERROR)
    {
        Log("[MarinerClient] DetourTransactionBegin failed: %ld\n", error);
        return false;
    }

    error = DetourUpdateThread(GetCurrentThread());
    if (error != NO_ERROR)
    {
        Log("[MarinerClient] DetourUpdateThread failed: %ld\n", error);
        return false;
    }

    error = DetourAttach((PVOID *)&Original_ConfigGetString, (PVOID)Hooked_ConfigGetString);
    if (error != NO_ERROR)
    {
        Log("[MarinerClient] DetourAttach ConfigGetString failed: %ld\n", error);
        return false;
    }

    error = DetourAttach((PVOID *)&Original_ConfigGetInt, (PVOID)Hooked_ConfigGetInt);
    if (error != NO_ERROR)
    {
        Log("[MarinerClient] DetourAttach ConfigGetInt failed: %ld\n", error);
        return false;
    }

    error = DetourTransactionCommit();
    if (error != NO_ERROR)
    {
        Log("[MarinerClient] DetourTransactionCommit failed: %ld\n", error);
        return false;
    }

    Log("[MarinerClient] All hooks installed\n");
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
    Log("[MarinerClient] DLL loaded! Image base: 0x%llX\n", (unsigned long long)g_imageBase);

    InitSections();
    InitDataSection();
    std::string root = GetGameRootPath();
    LoadIniFile(root + "Overrides.ini");
    LoadIniFile(root + "ClientOverrides.ini");

    if (!ResolveAddresses())
    {
        Log("[MarinerClient] ERROR: Address resolution failed!\n");
        return TRUE;
    }

    if (!InstallHooks())
    {
        Log("[MarinerClient] ERROR: Failed to install hooks!\n");
        return TRUE;
    }

    Log("[MarinerClient] Initialization complete\n");
    return TRUE;
}
