using LibHac.Common.FixedArrays;

namespace LibHac.Fs.Dbm
{
    public struct FileOptionalInfo
    {
        public Array8<byte> Data;
    }
}

namespace LibHac.Fs.Dbm.Impl
{
    public struct FileInfo
    {
        public uint BlockIndex;
        public Int64 Size;
        public FileOptionalInfo OptionalInfo;
    }

    public struct DirectoryInfo
    {
        public Array12<byte> Data;
    }
}