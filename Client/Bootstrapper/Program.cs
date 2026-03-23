using System.Runtime.InteropServices;
using System.Text;

namespace Mariner.Bootstrapper;

internal class Program
{
    #region Win32 PInvoke

    const int ERROR_SUCCESS = 0;
    const int ERROR_FILE_NOT_FOUND = 2;
    const int ERROR_GEN_FAILURE = 31;
    const int ERROR_BAD_ARGUMENTS = 160;
    const ulong IMAGE_ORDINAL_FLAG64 = 0x8000000000000000;

    [Flags]
    enum CreateProcessFlags : uint
    {
        CREATE_SUSPENDED = 0x00000004,
    }

    [StructLayout(LayoutKind.Sequential)]
    class STARTUPINFO
    {
        public int cb;
        public IntPtr lpReserved, lpDesktop, lpTitle;
        public int dwX, dwY, dwXSize, dwYSize;
        public int dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
        public STARTUPINFO() { cb = Marshal.SizeOf(this); }
    }

    [StructLayout(LayoutKind.Sequential)]
    class SECURITY_ATTRIBUTES
    {
        public int nLength;
        public IntPtr lpSecurityDescriptor;
        public bool bInheritHandle;
        public SECURITY_ATTRIBUTES() { nLength = Marshal.SizeOf(this); }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PROCESS_INFORMATION
    {
        public IntPtr hProcess, hThread;
        public int dwProcessId, dwThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PROCESS_BASIC_INFORMATION
    {
        public IntPtr Reserved1, PebBaseAddress, Reserved2_0, Reserved2_1, UniqueProcessId, Reserved3;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct IMAGE_DOS_HEADER { public ushort e_magic; unsafe fixed ushort _pad[29]; public int e_lfanew; }

    [StructLayout(LayoutKind.Sequential)]
    struct IMAGE_DATA_DIRECTORY { public uint VirtualAddress, Size; }

    [StructLayout(LayoutKind.Sequential)]
    unsafe struct IMAGE_OPTIONAL_HEADER64
    {
        public ushort Magic;
        public byte MajorLinkerVersion, MinorLinkerVersion;
        public uint SizeOfCode, SizeOfInitializedData, SizeOfUninitializedData;
        public uint AddressOfEntryPoint, BaseOfCode;
        public ulong ImageBase;
        public uint SectionAlignment, FileAlignment;
        public ushort MajorOperatingSystemVersion, MinorOperatingSystemVersion;
        public ushort MajorImageVersion, MinorImageVersion;
        public ushort MajorSubsystemVersion, MinorSubsystemVersion;
        public uint Win32VersionValue, SizeOfImage, SizeOfHeaders, CheckSum;
        public ushort Subsystem, DllCharacteristics;
        public ulong SizeOfStackReserve, SizeOfStackCommit, SizeOfHeapReserve, SizeOfHeapCommit;
        public uint LoaderFlags, NumberOfRvaAndSizes;
        public IMAGE_DATA_DIRECTORY ExportDir, ImportDir;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct IMAGE_NT_HEADERS64
    {
        public uint Signature;
        public ushort Machine, NumberOfSections;
        public uint TimeDateStamp;
        public uint PointerToSymbolTable, NumberOfSymbols;
        public ushort SizeOfOptionalHeader, Characteristics;
        public IMAGE_OPTIONAL_HEADER64 OptionalHeader;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct IMAGE_IMPORT_DESCRIPTOR
    {
        public uint OriginalFirstThunk, TimeDateStamp, ForwarderChain, Name, FirstThunk;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CreateProcessW(string? app, string? cmd, SECURITY_ATTRIBUTES? pa, SECURITY_ATTRIBUTES? ta, bool inherit, CreateProcessFlags flags, IntPtr env, string? dir, STARTUPINFO si, out PROCESS_INFORMATION pi);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern uint ResumeThread(IntPtr hThread);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool TerminateProcess(IntPtr hProcess, uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern unsafe bool ReadProcessMemory(IntPtr hProcess, IntPtr baseAddress, void* buffer, nuint size, nuint* bytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern unsafe bool WriteProcessMemory(IntPtr hProcess, IntPtr baseAddress, void* buffer, nuint size, nuint* bytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr address, nuint size, uint allocType, uint protect);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool VirtualProtectEx(IntPtr hProcess, IntPtr address, nuint size, uint newProtect, out uint oldProtect);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr GetModuleHandle(string? moduleName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

    [DllImport("ntdll.dll")]
    static extern int NtQueryInformationProcess(IntPtr handle, int infoClass, out PROCESS_BASIC_INFORMATION pbi, int size, out int returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, nuint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, out uint lpThreadId);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    const uint MEM_COMMIT = 0x1000;
    const uint MEM_RESERVE = 0x2000;
    const uint PAGE_READWRITE = 0x04;
    const uint PAGE_EXECUTE_READWRITE = 0x40;
    const uint INFINITE = 0xFFFFFFFF;

    #endregion

    static int Main(string[] args)
    {
        string exeDir = AppDomain.CurrentDomain.BaseDirectory;
        string binaryPath = Path.Combine(exeDir, "Mariner", "Binaries", "Win64", "Mariner-Win64-Shipping.exe");

        bool serverMode = false;
        string? libraryPath = null;
        var forwardedArgs = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--server", StringComparison.OrdinalIgnoreCase))
            {
                serverMode = true;
            }
            else if (args[i].Equals("--dll", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                libraryPath = args[++i];
            }
            else
            {
                forwardedArgs.Add(args[i]);
            }
        }

        // Auto-select DLL if not explicitly specified
        if (libraryPath == null)
        {
            string defaultDll = serverMode ? Path.Combine(exeDir, "MarinerServer.dll") : Path.Combine(exeDir, "MarinerClient.dll");
            if (File.Exists(defaultDll))
                libraryPath = defaultDll;
        }

        if (!File.Exists(binaryPath))
        {
            Console.Error.WriteLine($"Mariner: Binary not found: {binaryPath}");
            return ERROR_FILE_NOT_FOUND;
        }

        if (libraryPath != null && !File.Exists(libraryPath))
        {
            Console.Error.WriteLine($"Mariner: DLL not found: {libraryPath}");
            return ERROR_FILE_NOT_FOUND;
        }

        // Add implicit default arguments
        if (!forwardedArgs.Any(a => a.Equals("-noeac", StringComparison.OrdinalIgnoreCase)))
            forwardedArgs.Add("-noeac");

        if (serverMode)
        {
            string[] serverDefaults = ["-nullrhi", "-nosplash", "-nosound", "-nopause", "-unattended", "-log"];
            foreach (var arg in serverDefaults)
            {
                if (!forwardedArgs.Any(a => a.Equals(arg, StringComparison.OrdinalIgnoreCase)))
                    forwardedArgs.Add(arg);
            }
        }

        string commandLine = BuildCommandLine(forwardedArgs);
        bool needsInjection = libraryPath != null;

        // Sync steam_appid.txt from Overrides.ini AppId setting
        string binaryDir = Path.GetDirectoryName(binaryPath)!;
        string steamAppIdPath = Path.Combine(binaryDir, "steam_appid.txt");
        string overridesPath = Path.Combine(exeDir, "Overrides.ini");
        string? appId = ReadIniValue(overridesPath, "AppId");

        if (appId != null)
        {
            if (!File.Exists(steamAppIdPath) || File.ReadAllText(steamAppIdPath).Trim() != appId)
            {
                File.WriteAllText(steamAppIdPath, appId);
                Console.WriteLine($"Mariner: Set steam_appid.txt to {appId}");
            }
        }
        else if (File.Exists(steamAppIdPath))
        {
            File.Delete(steamAppIdPath);
            Console.WriteLine("Mariner: Removed steam_appid.txt (no AppId in Overrides.ini)");
        }

        var si = new STARTUPINFO();
        var sa = new SECURITY_ATTRIBUTES();
        var flags = needsInjection ? CreateProcessFlags.CREATE_SUSPENDED : 0;

        string? cmdLine = string.IsNullOrEmpty(commandLine) ? null : " " + commandLine;

        if (!CreateProcessW(binaryPath, cmdLine, sa, sa, false, flags, IntPtr.Zero, binaryDir, si, out var pi))
        {
            Console.Error.WriteLine($"Mariner: Failed to launch process. Error: {Marshal.GetLastWin32Error()}");
            return Marshal.GetLastWin32Error();
        }

        try
        {
            if (needsInjection)
            {
                Console.WriteLine($"Mariner: Injecting {Path.GetFileName(libraryPath)}...");

                if (!InjectDll(pi.hProcess, libraryPath!, out string injectError))
                {
                    TerminateProcess(pi.hProcess, (uint)ERROR_GEN_FAILURE);
                    Console.Error.WriteLine($"Mariner: DLL injection failed: {injectError}");
                    return ERROR_GEN_FAILURE;
                }

                Console.WriteLine($"Mariner: DLL injected into PID {pi.dwProcessId}, resuming main thread...");

                uint result = ResumeThread(pi.hThread);
                if (result == unchecked((uint)-1))
                {
                    TerminateProcess(pi.hProcess, (uint)ERROR_GEN_FAILURE);
                    Console.Error.WriteLine("Mariner: ResumeThread failed.");
                    return Marshal.GetLastWin32Error();
                }

                Console.WriteLine($"Mariner: Process launched successfully (PID {pi.dwProcessId}).");

                if (serverMode)
                {
                    CloseHandle(pi.hThread);
                    pi.hThread = IntPtr.Zero;
                    WaitForSingleObject(pi.hProcess, INFINITE);
                    Console.WriteLine("Mariner: Server process exited.");
                }
            }
            else
            {
                Console.WriteLine("Mariner: Client launched successfully.");
            }

            return ERROR_SUCCESS;
        }
        finally
        {
            if (pi.hThread != IntPtr.Zero) CloseHandle(pi.hThread);
            CloseHandle(pi.hProcess);
        }
    }

    static string BuildCommandLine(IEnumerable<string> args)
    {
        return string.Join(" ", args.Select(EscapeArg));
    }

    static string? ReadIniValue(string iniPath, string key)
    {
        if (!File.Exists(iniPath)) return null;
        foreach (var line in File.ReadLines(iniPath))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
                return trimmed[(key.Length + 1)..].Trim();
        }
        return null;
    }

    static string EscapeArg(string arg)
    {
        if (arg.Length == 0) return "\"\"";
        if (!arg.Any(c => char.IsWhiteSpace(c) || c == '"')) return arg;

        var sb = new StringBuilder();
        sb.Append('"');
        int backslashes = 0;
        foreach (char c in arg)
        {
            if (c == '\\') { backslashes++; }
            else if (c == '"') { sb.Append('\\', backslashes * 2 + 1); sb.Append('"'); backslashes = 0; }
            else { sb.Append('\\', backslashes); sb.Append(c); backslashes = 0; }
        }
        sb.Append('\\', backslashes * 2);
        sb.Append('"');
        return sb.ToString();
    }

    static unsafe bool InjectDll(IntPtr hProcess, string dllPath, out string errorMessage)
    {
        errorMessage = "";

        IntPtr kernel32 = GetModuleHandle("kernel32.dll");
        IntPtr loadLibraryW = GetProcAddress(kernel32, "LoadLibraryW");
        if (loadLibraryW == IntPtr.Zero) { errorMessage = "Failed to resolve LoadLibraryW."; return false; }

        string fullPath = Path.GetFullPath(dllPath);
        Console.WriteLine($"Mariner: DLL path: {fullPath}");

        uint pathSize = (uint)((fullPath.Length + 1) * sizeof(char));
        IntPtr remotePath = VirtualAllocEx(hProcess, IntPtr.Zero, pathSize, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
        if (remotePath == IntPtr.Zero) { errorMessage = "Failed to allocate remote memory for path."; return false; }

        fixed (byte* pathBytes = Encoding.Unicode.GetBytes(fullPath + '\0'))
        {
            if (!WriteProcessMemory(hProcess, remotePath, pathBytes, pathSize, null))
            { errorMessage = "Failed to write DLL path to remote process."; return false; }
        }

        IntPtr hThread = CreateRemoteThread(hProcess, IntPtr.Zero, 0, loadLibraryW, remotePath, 0, out _);
        if (hThread == IntPtr.Zero)
        {
            errorMessage = $"CreateRemoteThread failed. Error: {Marshal.GetLastWin32Error()}";
            return false;
        }

        uint waitResult = WaitForSingleObject(hThread, 10000);
        CloseHandle(hThread);

        if (waitResult != 0)
        {
            errorMessage = $"Timed out waiting for DLL load (wait result: {waitResult}).";
            return false;
        }

        Console.WriteLine("Mariner: CreateRemoteThread completed, DLL should be loaded.");
        return true;
    }
}
