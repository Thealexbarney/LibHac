namespace LibHac.Fs.Fsa;

// ReSharper disable once InconsistentNaming
public abstract class IAttributeFileSystem : IFileSystem
{
    public Result CreateDirectory(ref readonly Path path, NxFileAttributes archiveAttribute)
    {
        return DoCreateDirectory(in path, archiveAttribute);
    }

    public Result GetFileAttributes(out NxFileAttributes attributes, ref readonly Path path)
    {
        return DoGetFileAttributes(out attributes, in path);
    }

    public Result SetFileAttributes(ref readonly Path path, NxFileAttributes attributes)
    {
        return DoSetFileAttributes(in path, attributes);
    }

    public Result GetFileSize(out long fileSize, ref readonly Path path)
    {
        return DoGetFileSize(out fileSize, in path);
    }

    protected abstract Result DoCreateDirectory(ref readonly Path path, NxFileAttributes archiveAttribute);
    protected abstract Result DoGetFileAttributes(out NxFileAttributes attributes, ref readonly Path path);
    protected abstract Result DoSetFileAttributes(ref readonly Path path, NxFileAttributes attributes);
    protected abstract Result DoGetFileSize(out long fileSize, ref readonly Path path);
}