using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Capture.TeraModule.CameraFinder
{
    class MemoryScanner : IDisposable
    {
        // REQUIRED CONSTS

        const int PROCESS_QUERY_INFORMATION = 0x0400;
        const int MEM_COMMIT = 0x00001000;
        const int PAGE_READWRITE = 0x04;
        const int PROCESS_WM_READ = 0x0010;


        // REQUIRED METHODS

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern SafeProcessHandle OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadProcessMemory(SafeProcessHandle hProcess, uint lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);


        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int VirtualQueryEx(SafeProcessHandle hProcess, uint lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);


        internal sealed class SafeProcessHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern bool CloseHandle(IntPtr handle);

            private SafeProcessHandle()
                : base(true)
            {
            }

            internal SafeProcessHandle(IntPtr handle)
                : base(true)
            {
                SetHandle(handle);
            }

            protected override bool ReleaseHandle()
            {
                return CloseHandle(handle);
            }
        }

        public enum PageState
        {
            Commit = 0x1000,
            Free = 0x10000,
            Reserve = 0x2000
        }

        // REQUIRED STRUCTS
        public enum AllocationProtectEnum : uint
        {
            PAGE_EXECUTE = 0x00000010,
            PAGE_EXECUTE_READ = 0x00000020,
            PAGE_EXECUTE_READWRITE = 0x00000040,
            PAGE_EXECUTE_WRITECOPY = 0x00000080,
            PAGE_NOACCESS = 0x00000001,
            PAGE_READONLY = 0x00000002,
            PAGE_READWRITE = 0x00000004,
            PAGE_WRITECOPY = 0x00000008,
            PAGE_GUARD = 0x00000100,
            PAGE_NOCACHE = 0x00000200,
            PAGE_WRITECOMBINE = 0x00000400
        }

        public enum StateEnum : uint
        {
            MEM_COMMIT = 0x1000,
            MEM_FREE = 0x10000,
            MEM_RESERVE = 0x2000
        }

        public enum TypeEnum : uint
        {
            MEM_IMAGE = 0x1000000,
            MEM_MAPPED = 0x40000,
            MEM_PRIVATE = 0x20000
        }

        public struct MEMORY_BASIC_INFORMATION
        {
            public uint BaseAddress;
            public uint AllocationBase;
            public AllocationProtectEnum AllocationProtect;
            public uint RegionSize;
            public StateEnum State;
            public AllocationProtectEnum Protect;
            public TypeEnum Type;
        }


        private SafeProcessHandle processHandle;

        public IEnumerable<MEMORY_BASIC_INFORMATION> MemoryRegions()
        {
            uint proc_min_address = 0x7FFF0000;
            uint proc_max_address = 0xFFFFFFFF;
            uint current = proc_min_address;
            while (current < proc_max_address)
            {
                MEMORY_BASIC_INFORMATION mem_basic_info;
                int result = VirtualQueryEx(processHandle, current, out mem_basic_info, (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION)));
                yield return mem_basic_info;
                current += mem_basic_info.RegionSize;
            }
        }

        public byte[] ReadMemory(uint baseAddress, int size)
        {
            var stream = new MemoryStream(size);
            ReadMemory(stream, baseAddress, size);
            Debug.Assert(stream.GetBuffer().Length == stream.Length);
            return stream.GetBuffer();
        }

        public void ReadMemory(MemoryStream stream, uint baseAddress, int size)
        {
            stream.SetLength(size);
            int bytesRead = 0;

            // read everything in the buffer above
            bool success = ReadProcessMemory(processHandle, baseAddress, stream.GetBuffer(), size, ref bytesRead);
            if (!success)
                throw new Win32Exception();
            if (bytesRead != size)
                throw new Exception("Didn't read all bytes");
        }

        private static SafeProcessHandle OpenProcess(Process process)
        {
            var result = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_WM_READ, false, process.Id);
            if (result.IsInvalid)
                throw new Win32Exception();
            return result;
        }

        public MemoryScanner(Process process)
        {
            processHandle = OpenProcess(process);
        }

        public void Dispose()
        {
            if (processHandle != null)
            {
                processHandle.Close();
                processHandle = null;
            }
        }
    }
}

