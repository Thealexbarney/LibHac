using System.Collections.Generic;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Fs.Shim;
using LibHac.Tests.Fs.FileSystemClientTests;
using Xunit;

namespace LibHac.Tests.Fs.FsaTests
{
    public class MultiCommitTests
    {
        [Fact]
        public void Commit_MultipleFileSystems_AllFileSystemsAreCommitted()
        {
            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            var saveInfo = new List<(int id, string name)>
            {
                (1, "Save1"),
                (3, "Save2"),
                (2, "Save3")
            };

            foreach ((int id, string name) info in saveInfo)
            {
                var applicationId = new Ncm.ApplicationId((uint)info.id);
                fs.CreateSaveData(applicationId, UserId.InvalidId, 0, 0x4000, 0x4000, SaveDataFlags.None);
                fs.MountSaveData(info.name.ToU8Span(), applicationId, UserId.InvalidId);
            }

            foreach ((int id, string name) info in saveInfo)
            {
                fs.CreateFile($"{info.name}:/file{info.id}".ToU8Span(), 0);
            }

            var names = new List<U8String>();

            foreach ((int id, string name) info in saveInfo)
            {
                names.Add(info.name.ToU8String());
            }

            Assert.Success(fs.Commit(names.ToArray()));

            foreach ((int id, string name) info in saveInfo)
            {
                fs.Unmount(info.name.ToU8Span());
            }

            foreach ((int id, string name) info in saveInfo)
            {
                var applicationId = new Ncm.ApplicationId((uint)info.id);
                fs.MountSaveData(info.name.ToU8Span(), applicationId, UserId.InvalidId);
            }

            foreach ((int id, string name) info in saveInfo)
            {
                Assert.Success(fs.GetEntryType(out _, $"{info.name}:/file{info.id}".ToU8Span()));
            }
        }
    }
}
