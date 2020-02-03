using System.Diagnostics;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Tests.Fs.IFileSystemTestBase;

namespace LibHac.Tests.Fs
{
    public class SubdirectoryFileSystemTests : IFileSystemTests
    {
        protected override IFileSystem CreateFileSystem()
        {
            Trace.Listeners.Clear();
            var baseFs = new InMemoryFileSystem();
            baseFs.CreateDirectory("/sub");
            baseFs.CreateDirectory("/sub/path");

            var subFs = new SubdirectoryFileSystem(baseFs, "/sub/path");

            return subFs;
        }
    }
    public class SubdirectoryFileSystemTestsRoot : IFileSystemTests
    {
        protected override IFileSystem CreateFileSystem()
        {
            Trace.Listeners.Clear();
            var baseFs = new InMemoryFileSystem();

            var subFs = new SubdirectoryFileSystem(baseFs, "/");

            return subFs;
        }
    }
}
