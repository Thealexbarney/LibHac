using System;
using System.Diagnostics;

namespace LibHac.IO
{
    public ref struct PathParser
    {
        private ReadOnlySpan<char> _path;
        private int _offset;
        private int _length;
        private bool _finished;

        public PathParser(ReadOnlySpan<char> path)
        {
            Debug.Assert(PathTools.IsNormalized(path));

            if (path.Length < 1 || path[0] != '/')
            {
                throw new ArgumentException("Path must begin with a '/'");
            }

            _path = path;
            _offset = 0;
            _length = 0;
            _finished = false;
        }

        public bool TryGetNext(out ReadOnlySpan<char> name)
        {
            bool success = MoveNext();
            name = GetCurrent();
            return success;
        }

        public bool MoveNext()
        {
            if (_finished) return false;

            _offset = _offset + _length + 1;
            int end = _offset;

            while (end < _path.Length && _path[end] != '/')
            {
                end++;
            }

            _finished = end + 1 >= _path.Length;
            _length = end - _offset;

            return true;
        }

        public ReadOnlySpan<char> GetCurrent()
        {
            return _path.Slice(_offset, _length);
        }

        public bool IsFinished() => _finished;
    }
}
