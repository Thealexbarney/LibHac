using LibHac.Fs.Fsa;

namespace LibHac.Tests.Fs.IFileSystemTestBase
{
    public abstract partial class CommittableIFileSystemTests : IFileSystemTests
    {
        protected interface IReopenableFileSystemCreator
        {
            IFileSystem Create();
        }

        protected abstract IReopenableFileSystemCreator GetFileSystemCreator();
    }
}
