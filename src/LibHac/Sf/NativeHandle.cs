namespace LibHac.Sf
{
    // How should this be handled? Using a C# struct would be more accurate, but C#
    // doesn't have copy constructors or any way to prevent a struct from being copied.
    public class NativeHandle
    {
        public uint Handle { get; private set; }
        public bool IsManaged { get; private set; }

        public NativeHandle(uint handle)
        {
            Handle = handle;
        }

        public NativeHandle(uint handle, bool isManaged)
        {
            Handle = handle;
            IsManaged = isManaged;
        }
    }
}
