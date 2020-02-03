﻿using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Tests.Fs.IFileSystemTestBase;

namespace LibHac.Tests.Fs
{
    public class AesXtsFileSystemTests : IFileSystemTests
    {
        protected override IFileSystem CreateFileSystem()
        {
            var baseFs = new InMemoryFileSystem();

            var keys = new byte[0x20];
            var xtsFs = new AesXtsFileSystem(baseFs, keys, 0x4000);

            return xtsFs;
        }
    }
}
