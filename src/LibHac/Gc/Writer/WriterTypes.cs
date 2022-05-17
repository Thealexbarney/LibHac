namespace LibHac.Gc.Writer;

public enum AsicMode : byte
{
    Read = 0,
    Write = 1
}

public enum MemorySize
{
    // ReSharper disable InconsistentNaming
    Size1GB = 1,
    Size2GB = 2,
    Size4GB = 4,
    Size8GB = 8,
    Size16GB = 16,
    Size32GB = 32
    // ReSharper restore InconsistentNaming
}