using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace NandReaderGui
{
    public class DeviceStream : Stream
    {
        public const short FileAttributeNormal = 0x80;
        public const short InvalidHandleValue = -1;
        public const uint GenericRead = 0x80000000;
        public const uint GenericWrite = 0x40000000;
        public const uint CreateNew = 1;
        public const uint CreateAlways = 2;
        public const uint OpenExisting = 3;

        // Use interop to call the CreateFile function.
        // For more information about CreateFile,
        // see the unmanaged MSDN reference library.
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess,
          uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition,
          uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadFile(
            IntPtr hFile,                        // handle to file
            byte[] lpBuffer,                // data buffer
            int nNumberOfBytesToRead,        // number of bytes to read
            ref int lpNumberOfBytesRead,    // number of bytes read
            IntPtr lpOverlapped
            //
            // ref OVERLAPPED lpOverlapped        // overlapped buffer
        );

        private SafeFileHandle _handleValue;
        private FileStream _fs;

        public DeviceStream(string device, long length)
        {
            Load(device);
            Length = length;
        }

        private void Load(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException("path");
            }

            // Try to open the file.
            IntPtr ptr = CreateFile(path, GenericRead, 0, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);

            _handleValue = new SafeFileHandle(ptr, true);
            _fs = new FileStream(_handleValue, FileAccess.Read);

            // If the handle is invalid,
            // get the last Win32 error 
            // and throw a Win32Exception.
            if (_handleValue.IsInvalid)
            {
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
            }
        }

        public override bool CanRead { get; } = true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override void Flush() { }

        public override long Length { get; }

        public override long Position
        {
            get => _fs.Position;
            set => _fs.Position = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesRead = 0;
            var bufBytes = new byte[count];
            if (!ReadFile(_handleValue.DangerousGetHandle(), bufBytes, count, ref bytesRead, IntPtr.Zero))
            {
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
            }
            for (int i = 0; i < bytesRead; i++)
            {
                buffer[offset + i] = bufBytes[i];
            }
            return bytesRead;
        }

        public override int ReadByte()
        {
            int bytesRead = 0;
            var lpBuffer = new byte[1];
            if (!ReadFile(
                _handleValue.DangerousGetHandle(),                        // handle to file
                lpBuffer,                // data buffer
                1,        // number of bytes to read
                ref bytesRead,    // number of bytes read
                IntPtr.Zero
            ))
            { Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error()); }
            return lpBuffer[0];
        }

        public override long Seek(long offset, SeekOrigin origin) => _fs.Seek(offset, origin);

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override void Close()
        {
            _handleValue.Close();
            _handleValue.Dispose();
            _handleValue = null;
            base.Close();
        }

        private bool _disposed;

        protected override void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.
            if (!_disposed)
            {
                if (disposing)
                {
                    if (_handleValue != null)
                    {
                        _fs.Dispose();
                        _handleValue.Close();
                        _handleValue.Dispose();
                        _handleValue = null;
                    }
                }
                // Note disposing has been done.
                _disposed = true;
                base.Dispose(disposing);
            }
        }
    }
}
