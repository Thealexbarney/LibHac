using System;
using LibHac.Common;
using LibHac.Diag;
using static LibHac.Fs.StringTraits;

namespace LibHac.Fs.Common
{
    [NonCopyableDisposable]
    public ref struct DirectoryPathParser
    {
        private Span<byte> _buffer;
        private byte _replacedChar;
        private int _position;

        // Todo: Make private so we can use the GetCurrentPath method once lifetime tracking is better
        public Path CurrentPath;

        public void Dispose()
        {
            CurrentPath.Dispose();
        }

        public Result Initialize(ref Path path)
        {
            Span<byte> pathBuffer = path.GetWriteBufferLength() != 0 ? path.GetWriteBuffer() : Span<byte>.Empty;

            int windowsSkipLength = WindowsPath.GetWindowsSkipLength(pathBuffer);
            _buffer = pathBuffer.Slice(windowsSkipLength);

            if (windowsSkipLength != 0)
            {
                Result rc = CurrentPath.InitializeWithNormalization(pathBuffer, windowsSkipLength + 1);
                if (rc.IsFailure()) return rc;

                _buffer = _buffer.Slice(1);
            }
            else
            {
                Span<byte> initialPath = ReadNextImpl();

                if (!initialPath.IsEmpty)
                {
                    Result rc = CurrentPath.InitializeWithNormalization(initialPath);
                    if (rc.IsFailure()) return rc;
                }
            }

            return Result.Success;
        }

        // Todo: Return reference when escape semantics are better
        //public ref readonly Path GetCurrentPath()
        //{
        //    return ref CurrentPath;
        //}

        public Result ReadNext(out bool isFinished)
        {
            isFinished = false;

            Span<byte> nextEntry = ReadNextImpl();

            if (nextEntry.IsEmpty)
            {
                isFinished = true;
                return Result.Success;
            }

            return CurrentPath.AppendChild(nextEntry);
        }

        private Span<byte> ReadNextImpl()
        {
            // Check if we've already hit the end of the path.
            if (_position < 0 || _buffer.At(0) == 0)
                return Span<byte>.Empty;

            // Restore the character we previously replaced with a null terminator.
            if (_replacedChar != 0)
            {
                _buffer[_position] = _replacedChar;

                if (_replacedChar == DirectorySeparator)
                    _position++;
            }

            // If the path is rooted, the first entry should be the root directory.
            if (_position == 0 && _buffer.At(0) == DirectorySeparator)
            {
                _replacedChar = _buffer[1];
                _buffer[1] = 0;
                _position = 1;
                return _buffer;
            }

            // Find the end of the next entry, replacing the directory separator with a null terminator.
            Span<byte> entry = _buffer.Slice(_position);

            int i;
            for (i = _position; _buffer.At(i) != DirectorySeparator; i++)
            {
                if (_buffer.At(i) == 0)
                {
                    if (i == _position)
                        entry = Span<byte>.Empty;

                    _position = -1;
                    return entry;
                }
            }

            Assert.SdkAssert(_buffer.At(i + 1) != NullTerminator);

            _replacedChar = DirectorySeparator;
            _buffer[i] = 0;
            _position = i;
            return entry;
        }
    }
}
