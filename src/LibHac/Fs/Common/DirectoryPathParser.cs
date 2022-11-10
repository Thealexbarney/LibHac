using System;
using LibHac.Common;
using LibHac.Diag;
using static LibHac.Fs.StringTraits;

// ReSharper disable once CheckNamespace
namespace LibHac.Fs;

/// <summary>
/// Iterates through each directory in a path beginning with the root directory.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
[NonCopyableDisposable]
public ref struct DirectoryPathParser
{
    private Span<byte> _buffer;
    private byte _replacedChar;
    private int _position;

    // Todo: Make private so we can use the GetCurrentPath method once lifetime tracking is better
    public Path CurrentPath;

    public DirectoryPathParser()
    {
        _buffer = Span<byte>.Empty;
        _replacedChar = 0;
        _position = 0;
        CurrentPath = new Path();
    }

    public void Dispose()
    {
        CurrentPath.Dispose();
    }

    /// <summary>
    /// Initializes this <see cref="DirectoryPathParser"/> with a new <see cref="Path"/>. The <see cref="Path"/>
    /// should not be a fixed path that was just initialized with <see cref="PathFunctions.SetUpFixedPath"/>
    /// because we need it to have an allocated write buffer.
    /// </summary>
    /// <param name="path">The <see cref="Path"/> to iterate. Must have an allocated write buffer.</param>
    /// <returns>The <see cref="Result"/> of the operation.</returns>
    public Result Initialize(ref Path path)
    {
        Span<byte> pathBuffer = path.GetWriteBufferLength() != 0 ? path.GetWriteBuffer() : Span<byte>.Empty;

        int windowsSkipLength = WindowsPath.GetWindowsSkipLength(pathBuffer);
        _buffer = pathBuffer.Slice(windowsSkipLength);

        if (windowsSkipLength != 0)
        {
            Result res = CurrentPath.InitializeWithNormalization(pathBuffer, windowsSkipLength + 1);
            if (res.IsFailure()) return res.Miss();

            _buffer = _buffer.Slice(1);
        }
        else
        {
            Span<byte> initialPath = ReadNextImpl();

            if (!initialPath.IsEmpty)
            {
                Result res = CurrentPath.InitializeWithNormalization(initialPath);
                if (res.IsFailure()) return res.Miss();
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