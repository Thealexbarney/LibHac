namespace LibHac.Fs.Fsa;

// ReSharper disable once InconsistentNaming
public abstract class IAttributeFileSystem : IFileSystem
{
    public Result CreateDirectory(in Path path, NxFileAttributes archiveAttribute)
    {
        return DoCreateDirectory(in path, archiveAttribute);
    }

    public Result GetFileAttributes(out NxFileAttributes attributes, in Path path)
    {
        return DoGetFileAttributes(out attributes, in path);
    }

    public Result SetFileAttributes(in Path path, NxFileAttributes attributes)
    {
        return DoSetFileAttributes(in path, attributes);
    }

    public Result GetFileSize(out long fileSize, in Path path)
    {
        return DoGetFileSize(out fileSize, in path);
    }

    protected abstract Result DoCreateDirectory(in Path path, NxFileAttributes archiveAttribute);
    protected abstract Result DoGetFileAttributes(out NxFileAttributes attributes, in Path path);
    protected abstract Result DoSetFileAttributes(in Path path, NxFileAttributes attributes);
    protected abstract Result DoGetFileSize(out long fileSize, in Path path);
}