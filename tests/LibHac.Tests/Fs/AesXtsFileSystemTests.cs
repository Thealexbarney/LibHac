using LibHac.Fs.Fsa;
using LibHac.Tests.Fs.IFileSystemTestBase;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;

namespace LibHac.Tests.Fs;

public class AesXtsFileSystemTests : IFileSystemTests
{
    protected override IFileSystem CreateFileSystem()
    {
        var baseFs = new InMemoryFileSystem();

        byte[] keys = new byte[0x20];
        var xtsFs = new AesXtsFileSystem(baseFs, keys, 0x4000);

        return xtsFs;
    }
}