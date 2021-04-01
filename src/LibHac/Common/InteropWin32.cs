using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#pragma warning disable 649

namespace LibHac.Common
{
    public static unsafe class InteropWin32
    {
        [DllImport("kernel32.dll")]
        private static extern int MultiByteToWideChar(uint codePage, uint dwFlags, byte* lpMultiByteStr,
            int cbMultiByte, char* lpWideCharStr, int cchWideChar);

        public static int MultiByteToWideChar(int codePage, ReadOnlySpan<byte> bytes, Span<char> chars)
        {
            fixed (byte* pBytes = bytes)
            fixed (char* pChars = chars)
            {
                return MultiByteToWideChar((uint)codePage, 0, pBytes, bytes.Length, pChars, chars.Length);
            }
        }

        [DllImport("kernel32.dll")]
        public static extern bool FindClose(IntPtr handle);

        [DllImport("kernel32.dll")]
        public static extern IntPtr FindFirstFileW(char* lpFileName, Win32FindData* lpFindFileData);

        public static IntPtr FindFirstFileW(ReadOnlySpan<char> fileName, out Win32FindData findFileData)
        {
            fixed (char* pfileName = fileName)
            {
                Unsafe.SkipInit(out findFileData);
                return FindFirstFileW(pfileName, (Win32FindData*)Unsafe.AsPointer(ref findFileData));
            }
        }

        public struct Win32FindData
        {
            public uint FileAttributes;
            private uint _creationTimeLow;
            private uint _creationTimeHigh;
            private uint _lastAccessLow;
            private uint _lastAccessHigh;
            private uint _lastWriteLow;
            private uint _lastWriteHigh;
            private uint _fileSizeHigh;
            private uint _fileSizeLow;
            public uint Reserved0;
            public uint Reserved1;
            private fixed char _fileName[260];
            private fixed char _alternateFileName[14];

            public long CreationTime => (long)((ulong)_creationTimeHigh << 32 | _creationTimeLow);
            public long LastAccessTime => (long)((ulong)_lastAccessHigh << 32 | _lastAccessLow);
            public long LastWriteTime => (long)((ulong)_lastWriteHigh << 32 | _lastWriteLow);
            public long FileSize => (long)_fileSizeHigh << 32 | _fileSizeLow;

            public Span<char> FileName => MemoryMarshal.CreateSpan(ref _fileName[0], 260);
            public Span<char> AlternateFileName => MemoryMarshal.CreateSpan(ref _alternateFileName[0], 14);
        }
    }
}
