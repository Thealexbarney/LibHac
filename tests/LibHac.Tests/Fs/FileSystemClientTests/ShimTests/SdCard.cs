using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Fs.Shim;
using Xunit;

namespace LibHac.Tests.Fs.FileSystemClientTests.ShimTests
{
    public class SdCard
    {
        [Fact]
        public void MountSdCard_CardIsInserted_Succeeds()
        {
            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            Assert.Success(fs.MountSdCard("sdcard".ToU8Span()));
        }

        [Fact]
        public void MountSdCard_CardIsNotInserted_Fails()
        {
            FileSystemClient fs = FileSystemServerFactory.CreateClient(false);

            Assert.Result(ResultFs.PortSdCardNoDevice, fs.MountSdCard("sdcard".ToU8Span()));
        }

        [Fact]
        public void MountSdCard_CanWriteToFsAfterMounted()
        {
            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            fs.MountSdCard("sdcard".ToU8String());

            Assert.Success(fs.CreateFile("sdcard:/file".ToU8Span(), 100, CreateFileOptions.None));
        }

        [Fact]
        public void IsSdCardInserted_CardIsInserted_ReturnsTrue()
        {
            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            Assert.True(fs.IsSdCardInserted());
        }

        [Fact]
        public void IsSdCardInserted_CardIsNotInserted_ReturnsFalse()
        {
            FileSystemClient fs = FileSystemServerFactory.CreateClient(false);

            Assert.False(fs.IsSdCardInserted());
        }

        [Fact]
        public void IsSdCardAccessible_CardIsInserted_ReturnsTrue()
        {
            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            Assert.True(fs.IsSdCardAccessible());
        }

        [Fact]
        public void IsSdCardAccessible_CardIsNotInserted_ReturnsFalse()
        {
            FileSystemClient fs = FileSystemServerFactory.CreateClient(false);

            Assert.False(fs.IsSdCardAccessible());
        }

        [Fact]
        public void SetSdCardAccessibility_SetAccessibilityPersists()
        {
            FileSystemClient fs = FileSystemServerFactory.CreateClient(false);

            fs.SetSdCardAccessibility(true);
            Assert.True(fs.IsSdCardAccessible());

            fs.SetSdCardAccessibility(false);
            Assert.False(fs.IsSdCardAccessible());
        }
    }
}
