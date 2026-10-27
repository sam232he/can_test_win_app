using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Text;

namespace USBCANTool
{
    /// <summary>
    /// P/Invoke wrapper for ZLGCAN (zlgcan.dll).
    /// Always place the matching-architecture zlgcan.dll next to the EXE.
    /// </summary>
    public static class ZlgCanApi
    {
        public const uint STATUS_ERR = 0;
        public const uint STATUS_OK = 1;
        public const uint STATUS_ONLINE = 2;
        public const uint STATUS_OFFLINE = 3;

        public const int INVALID_DEVICE_HANDLE = 0;
        public const int INVALID_CHANNEL_HANDLE = 0;

        public const uint ZCAN_USBCAN1 = 3;
        public const uint ZCAN_USBCAN2 = 4;
        public const uint ZCAN_USBCAN_E_U = 20;
        public const uint ZCAN_USBCAN_2E_U = 21;
        public const uint ZCAN_USBCAN_4E_U = 31;
        public const uint ZCAN_USBCAN_8E_U = 34;

        public const uint TYPE_CAN = 0;
        public const uint TYPE_CANFD = 1;

        public const uint CAN_EFF_FLAG = 0x80000000;
        public const uint CAN_RTR_FLAG = 0x40000000;
        public const uint CAN_ERR_FLAG = 0x20000000;
        public const uint CAN_SFF_MASK = 0x000007FF; // standard 11-bit
        public const uint CAN_EFF_MASK = 0x1FFFFFFF; // extended 29-bit
        public const uint CAN_ID_FLAG = CAN_EFF_MASK;

        private static IntPtr _hModule = IntPtr.Zero;
        private static string _loadError = null;

        public static uint MAKE_CAN_ID(uint id, bool eff, bool rtr, bool err)
        {
            uint result = id & (eff ? CAN_EFF_MASK : CAN_SFF_MASK);
            if (eff) result |= CAN_EFF_FLAG;
            if (rtr) result |= CAN_RTR_FLAG;
            if (err) result |= CAN_ERR_FLAG;
            return result;
        }

        public static uint GET_ID(uint canId) =>
            canId & (IS_EFF(canId) ? CAN_EFF_MASK : CAN_SFF_MASK);
        public static bool IS_EFF(uint canId) => (canId & CAN_EFF_FLAG) != 0;
        public static bool IS_RTR(uint canId) => (canId & CAN_RTR_FLAG) != 0;

        [StructLayout(LayoutKind.Sequential)]
        public struct can_frame
        {
            public uint can_id;
            public byte can_dlc;
            public byte __pad;
            public byte __res0;
            public byte __res1;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] data;
        }

        // Total size 32 bytes (can_type + canfd union size)
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ZCAN_CHANNEL_INIT_CONFIG
        {
            public uint can_type;
            public uint acc_code;
            public uint acc_mask;
            public uint reserved;
            public byte filter;
            public byte timing0;
            public byte timing1;
            public byte mode;
            public uint pad1;
            public uint pad2;
            public uint pad3;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ZCAN_Transmit_Data
        {
            public can_frame frame;
            public uint transmit_type;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ZCAN_Receive_Data
        {
            public can_frame frame;
            public ulong timestamp;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ZCAN_DEVICE_INFO
        {
            public ushort hw_Version;
            public ushort fw_Version;
            public ushort dr_Version;
            public ushort in_Version;
            public ushort irq_Num;
            public byte can_Num;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public byte[] str_Serial_Num;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 40)]
            public byte[] str_hw_Type;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public ushort[] reserved;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        /// <summary>
        /// Process bitness (true = 64-bit process).
        /// </summary>
        public static bool Is64BitProcess => Environment.Is64BitProcess;

        public static string ProcessArch => Is64BitProcess ? "x64" : "x86";

        /// <summary>
        /// Full path of the EXE folder (where zlgcan.dll must live).
        /// </summary>
        public static string ExeDirectory
        {
            get
            {
                string path = AppDomain.CurrentDomain.BaseDirectory;
                if (string.IsNullOrEmpty(path))
                    path = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                return path ?? ".";
            }
        }

        public static string DllPath => Path.Combine(ExeDirectory, "zlgcan.dll");

        public static bool DllFileExists => File.Exists(DllPath);

        public static string LastLoadError => _loadError;

        /// <summary>
        /// Explicitly load zlgcan.dll from the EXE directory.
        /// Call once before any API. Returns true on success.
        /// </summary>
        public static bool EnsureLoaded()
        {
            if (_hModule != IntPtr.Zero)
                return true;

            string dir = ExeDirectory;
            string dll = Path.Combine(dir, "zlgcan.dll");

            if (!File.Exists(dll))
            {
                _loadError = "File not found: " + dll;
                return false;
            }

            // The EXE directory is already searched by Windows; set the vendor subfolder
            // as the additional search directory when the package provides one.
            string kernel = Path.Combine(dir, "kerneldlls");
            string dependencyDirectory = Directory.Exists(kernel) ? kernel : dir;
            if (!SetDllDirectory(dependencyDirectory))
            {
                int err = Marshal.GetLastWin32Error();
                _loadError = string.Format(
                    "Could not configure the native DLL dependency search directory:\n{0}\nWin32 error = {1}",
                    dependencyDirectory, err);
                return false;
            }

            _hModule = LoadLibrary(dll);
            if (_hModule == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                // 193 = ERROR_BAD_EXE_FORMAT → architecture mismatch
                if (err == 193)
                {
                    _loadError = string.Format(
                        "Architecture mismatch (Win32 error 193).\n" +
                        "This process is {0}, but zlgcan.dll is the other bitness.\n" +
                        "Rebuild the app as {0} (or use the matching zlgcan.dll).",
                        ProcessArch);
                }
                else
                {
                    _loadError = string.Format(
                        "LoadLibrary failed for:\n{0}\nWin32 error = {1}\n" +
                        "Often means a dependency DLL is missing (VC runtime or files inside kerneldlls).",
                        dll, err);
                }
                return false;
            }

            _loadError = null;
            return true;
        }

        public static string Diagnose()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Process architecture : " + ProcessArch + " (" + (Is64BitProcess ? "64-bit" : "32-bit") + ")");
            sb.AppendLine("EXE folder           : " + ExeDirectory);
            sb.AppendLine("Expected DLL path    : " + DllPath);
            sb.AppendLine("zlgcan.dll exists    : " + (DllFileExists ? "YES" : "NO"));
            if (DllFileExists)
            {
                try
                {
                    var fi = new FileInfo(DllPath);
                    sb.AppendLine("zlgcan.dll size      : " + fi.Length + " bytes");
                }
                catch { }
            }
            string k = Path.Combine(ExeDirectory, "kerneldlls");
            sb.AppendLine("kerneldlls folder    : " + (Directory.Exists(k) ? "YES" : "NO"));
            AppendVCRuntimeStatus(sb);
            AppendMissingDependencies(sb);
            if (_loadError != null)
                sb.AppendLine("Load error           : " + _loadError);
            return sb.ToString();
        }

        private static void AppendVCRuntimeStatus(StringBuilder sb)
        {
            try
            {
                using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey runtime = hklm.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64"))
                {
                    if (runtime == null || Convert.ToInt32(runtime.GetValue("Installed", 0)) != 1)
                    {
                        sb.AppendLine("VC++ 2015-2022 x64   : NOT INSTALLED");
                        return;
                    }

                    object version = runtime.GetValue("Version");
                    sb.AppendLine("VC++ 2015-2022 x64   : INSTALLED" +
                        (version is string ? " (" + version + ")" : ""));
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException ||
                ex is System.Security.SecurityException || ex is IOException)
            {
                sb.AppendLine("VC++ 2015-2022 x64   : CHECK FAILED (" + ex.Message + ")");
            }
        }

        private static void AppendMissingDependencies(StringBuilder sb)
        {
            if (!DllFileExists)
                return;

            var pending = new Stack<string>();
            var inspected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var missing = new List<string>();
            pending.Push(DllPath);

            while (pending.Count > 0)
            {
                string modulePath = pending.Pop();
                if (!inspected.Add(modulePath))
                    continue;

                try
                {
                    ushort machine;
                    List<string> imports = ReadImports(modulePath, out machine);
                    if ((Is64BitProcess && machine != 0x8664) || (!Is64BitProcess && machine != 0x014c))
                    {
                        missing.Add(Path.GetFileName(modulePath) + " has the wrong architecture");
                        continue;
                    }

                    string moduleDirectory = Path.GetDirectoryName(modulePath);
                    foreach (string import in imports)
                    {
                        if (import.StartsWith("api-ms-win-", StringComparison.OrdinalIgnoreCase) ||
                            import.StartsWith("ext-ms-win-", StringComparison.OrdinalIgnoreCase))
                            continue;

                        string dependencyPath = FindDependency(import, moduleDirectory);
                        if (dependencyPath == null)
                        {
                            missing.Add(Path.GetFileName(modulePath) + " -> " + import);
                            continue;
                        }

                        if (IsBundledDependency(dependencyPath))
                            pending.Push(dependencyPath);
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException ||
                    ex is InvalidDataException || ex is ArgumentException)
                {
                    missing.Add(Path.GetFileName(modulePath) + " (could not inspect imports: " + ex.Message + ")");
                }
            }

            sb.AppendLine("Native dependencies  :");
            if (missing.Count == 0)
            {
                sb.AppendLine("  No missing bundled/imported DLLs detected.");
                return;
            }

            foreach (string dependency in missing)
                sb.AppendLine("  MISSING/INVALID: " + dependency);
        }

        private static string FindDependency(string name, string importingDirectory)
        {
            var directories = new List<string>
            {
                importingDirectory,
                ExeDirectory,
                Path.Combine(ExeDirectory, "kerneldlls"),
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                Environment.GetFolderPath(Environment.SpecialFolder.Windows)
            };
            string pathValue = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathValue))
                directories.AddRange(pathValue.Split(Path.PathSeparator));

            foreach (string directory in directories)
            {
                if (string.IsNullOrEmpty(directory))
                    continue;
                string candidate = Path.Combine(directory.Trim('"'), name);
                if (File.Exists(candidate))
                    return candidate;
            }
            return null;
        }

        private static bool IsBundledDependency(string path)
        {
            string fullPath = Path.GetFullPath(path);
            string kernelDirectory = Path.GetFullPath(Path.Combine(ExeDirectory, "kerneldlls"));
            string exeDirectory = Path.GetFullPath(ExeDirectory);
            if (!kernelDirectory.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                kernelDirectory += Path.DirectorySeparatorChar;
            if (!exeDirectory.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                exeDirectory += Path.DirectorySeparatorChar;
            return fullPath.StartsWith(kernelDirectory, StringComparison.OrdinalIgnoreCase) ||
                fullPath.StartsWith(exeDirectory, StringComparison.OrdinalIgnoreCase);
        }

        private static List<string> ReadImports(string path, out ushort machine)
        {
            var imports = new List<string>();
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new BinaryReader(stream))
            {
                stream.Position = 0x3c;
                int peOffset = reader.ReadInt32();
                stream.Position = peOffset;
                if (reader.ReadUInt32() != 0x00004550)
                    throw new InvalidDataException("Invalid PE signature");

                machine = reader.ReadUInt16();
                ushort sectionCount = reader.ReadUInt16();
                stream.Position += 12;
                ushort optionalHeaderSize = reader.ReadUInt16();
                stream.Position += 2;
                long optionalHeader = stream.Position;
                ushort magic = reader.ReadUInt16();
                bool isPe32Plus = magic == 0x20b;
                if (!isPe32Plus && magic != 0x10b)
                    throw new InvalidDataException("Unsupported PE optional header");

                stream.Position = optionalHeader + (isPe32Plus ? 112 : 96) + 8;
                uint importRva = reader.ReadUInt32();
                if (importRva == 0)
                    return imports;

                long sectionTable = optionalHeader + optionalHeaderSize;
                var sections = new List<PeSection>();
                for (int i = 0; i < sectionCount; i++)
                {
                    stream.Position = sectionTable + i * 40 + 8;
                    sections.Add(new PeSection
                    {
                        VirtualSize = reader.ReadUInt32(),
                        VirtualAddress = reader.ReadUInt32(),
                        RawSize = reader.ReadUInt32(),
                        RawAddress = reader.ReadUInt32()
                    });
                }

                long importOffset = RvaToOffset(importRva, sections, stream.Length);
                for (int i = 0; i < 4096; i++)
                {
                    stream.Position = importOffset + i * 20L;
                    if (stream.Position + 20 > stream.Length)
                        throw new InvalidDataException("Import table is truncated");
                    reader.ReadUInt32();
                    reader.ReadUInt32();
                    reader.ReadUInt32();
                    uint nameRva = reader.ReadUInt32();
                    reader.ReadUInt32();
                    if (nameRva == 0)
                        break;

                    stream.Position = RvaToOffset(nameRva, sections, stream.Length);
                    var name = new StringBuilder();
                    for (int j = 0; j < 260 && stream.Position < stream.Length; j++)
                    {
                        byte value = reader.ReadByte();
                        if (value == 0)
                            break;
                        name.Append((char)value);
                    }
                    if (name.Length == 0)
                        throw new InvalidDataException("Invalid import name");
                    imports.Add(name.ToString());
                }
            }
            return imports;
        }

        private static long RvaToOffset(uint rva, List<PeSection> sections, long fileLength)
        {
            foreach (PeSection section in sections)
            {
                uint size = Math.Max(section.VirtualSize, section.RawSize);
                if (rva >= section.VirtualAddress && rva - section.VirtualAddress < size)
                {
                    long offset = section.RawAddress + (long)(rva - section.VirtualAddress);
                    if (offset >= 0 && offset < fileLength)
                        return offset;
                }
            }
            throw new InvalidDataException("Import table points outside the file");
        }

        private sealed class PeSection
        {
            public uint VirtualSize;
            public uint VirtualAddress;
            public uint RawSize;
            public uint RawAddress;
        }

        // ---- Native functions (stdcall). Names must match exports in zlgcan.dll ----
        [DllImport("zlgcan.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public static extern IntPtr ZCAN_OpenDevice(uint device_type, uint device_index, uint reserved);

        [DllImport("zlgcan.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern uint ZCAN_CloseDevice(IntPtr device_handle);

        [DllImport("zlgcan.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern uint ZCAN_GetDeviceInf(IntPtr device_handle, ref ZCAN_DEVICE_INFO pInfo);

        [DllImport("zlgcan.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public static extern uint ZCAN_SetValue(IntPtr device_handle, string path, string value);

        [DllImport("zlgcan.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr ZCAN_InitCAN(IntPtr device_handle, uint can_index, ref ZCAN_CHANNEL_INIT_CONFIG pInitConfig);

        [DllImport("zlgcan.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern uint ZCAN_StartCAN(IntPtr channel_handle);

        [DllImport("zlgcan.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern uint ZCAN_ResetCAN(IntPtr channel_handle);

        [DllImport("zlgcan.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern uint ZCAN_ClearBuffer(IntPtr channel_handle);

        [DllImport("zlgcan.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern uint ZCAN_Transmit(IntPtr channel_handle, ref ZCAN_Transmit_Data pTransmit, uint len);

        [DllImport("zlgcan.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern uint ZCAN_Receive(IntPtr channel_handle, [In, Out] ZCAN_Receive_Data[] pReceive, uint len, int wait_time);

        [DllImport("zlgcan.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern uint ZCAN_GetReceiveNum(IntPtr channel_handle, uint type);
    }
}
