using LibHac.Fs;

namespace LibHac.Tests.Fs.IFileSystemTestBase
{
    public abstract partial class IFileSystemTests
    {
        protected abstract IFileSystem CreateFileSystem();
    }
}
