namespace LibHac.Fs
{
    public static class PathTool
    {
        // These are kept in nn::fs, but C# requires them to be inside a class
        internal const int EntryNameLengthMax = 0x300;
        internal const int MountNameLengthMax = 15;
    }
}
