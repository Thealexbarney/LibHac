using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Tests.Fs.IFileSystemTestBase;

namespace LibHac.Tests.Fs
{
    public class InMemoryFileSystemTests : IAttributeFileSystemTests
    {
        protected override IFileSystem CreateFileSystem()
        {
            return new InMemoryFileSystem();
        }

        protected override IAttributeFileSystem CreateAttributeFileSystem()
        {
            return new InMemoryFileSystem();
        }
    }
}
