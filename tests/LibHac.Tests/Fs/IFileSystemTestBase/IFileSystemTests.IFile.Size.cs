using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using Xunit;

namespace LibHac.Tests.Fs.IFileSystemTestBase;

public abstract partial class IFileSystemTests
{
    [Fact]
    public void SetSize_FileSizeModified()
    {
        IFileSystem fs = CreateFileSystem();
        fs.CreateFile("/file", 0, CreateFileOptions.None);

        using var file = new UniqueRef<IFile>();
        fs.OpenFile(ref file.Ref(), "/file", OpenMode.All);
        Result res = file.Get.SetSize(54321);
        file.Reset();

        fs.OpenFile(ref file.Ref(), "/file", OpenMode.All);
        file.Get.GetSize(out long fileSize);

        Assert.Success(res);
        Assert.Equal(54321, fileSize);
    }
}
