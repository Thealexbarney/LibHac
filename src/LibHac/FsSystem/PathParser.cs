using System;
using System.Diagnostics;

namespace LibHac.FsSystem
{
    /// <summary>
    /// Enumerates a file or directory path one segment at a time.
    /// </summary>
    /// <remarks>When the parser is initialized <see cref="GetCurrent"/>
    /// will return the root directory name, i.e. an empty string.</remarks>
    public ref struct PathParser
    {
        private ReadOnlySpan<byte> _path;
        private int _offset;
        private int _length;
        private bool _finished;

        public PathParser(ReadOnlySpan<byte> path)
        {
            Debug.Assert(PathTools.IsNormalized(path));

            if (path.Length < 1 || path[0] != '/')
            {
                throw new ArgumentException("Path must begin with a '/'");
            }

            _path = path;
            _offset = 0;
            _length = 0;
            _finished = path.Length == 1 || path[1] == '\0';
        }

        /// <summary>
        /// Moves the iterator to the next segment in the path and gets the name of that segment.
        /// </summary>
        /// <param name="name">When this method returns, contains the path segment's name.</param>
        /// <returns><see langword="true"/> if the <see cref="PathParser"/> was able to
        /// move to the next path segment.
        /// <see langword="false"/> if there are no remaining path segments.</returns>
        public bool TryGetNext(out ReadOnlySpan<byte> name)
        {
            bool success = MoveNext();
            name = GetCurrent();
            return success;
        }

        /// <summary>
        /// Moves the iterator to the next segment in the path.
        /// </summary>
        /// <returns><see langword="true"/> if the <see cref="PathParser"/> was able to
        /// move to the next path segment.
        /// <see langword="false"/> if there are no remaining path segments.</returns>
        public bool MoveNext()
        {
            if (_finished) return false;

            _offset = _offset + _length + 1;
            int end = _offset;

            while (end < _path.Length && _path[end] != '\0' && _path[end] != '/')
            {
                end++;
            }

            _finished = end + 1 >= _path.Length || _path[end] == '\0';
            _length = end - _offset;

            return true;
        }

        /// <summary>
        /// Gets the current path segment's name.
        /// </summary>
        /// <returns>The current path segment.</returns>
        public ReadOnlySpan<byte> GetCurrent()
        {
            return _path.Slice(_offset, _length);
        }

        /// <summary>
        /// Checks if the current path segment is the final one.
        /// </summary>
        /// <returns><see langword="true"/> if the current path segment is the final one.</returns>
        public bool IsFinished() => _finished;
    }
}
