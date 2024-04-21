using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Tests.Fs;
using Xunit;

namespace LibHac.Tests.FsSystem;

public class AlignmentMatchingFileTests
{
    [Fact]
    public void ReadWrite_AccessCorrectnessTestAgainstMemoryStorage()
    {
        SetupRandomAccessTest().Run(1000);
    }

    private StorageTester SetupRandomAccessTest()
    {
        byte[] fileBuffer = new byte[0x180000];
        byte[] referenceBuffer = new byte[0x180000];

        fileBuffer.AsSpan().Fill(0x55);
        referenceBuffer.AsSpan().Fill(0x55);

        var referenceStorage = new MemoryStorage(referenceBuffer);

        using var baseFile = new UniqueRef<IFile>(new StorageFile(new MemoryStorage(fileBuffer), OpenMode.All));
        var alignmentFile = new AlignmentMatchingFile(ref baseFile.Ref, OpenMode.All);
        var alignmentStorage = new FileStorage(alignmentFile);

        var referenceEntry = new StorageTester.Entry(referenceStorage, referenceBuffer);
        var alignmentEntry = new StorageTester.Entry(alignmentStorage, fileBuffer);

        var testerConfig = new StorageTester.Configuration
        {
            Entries = [referenceEntry, alignmentEntry],
            AccessParams = [
                new(50, 0x100),
                new(50, 0x4000),
                new(50, 0, 0x40000, 0x100, 0x100),
                new(50, 0, 0x4000, 0x100, 0)
            ],
            TaskProbs = [50, 50, 1],
            AccessTypeProbs = [10, 10, 5],
            RngSeed = 64972,
            FrequentAccessBlockCount = 3
        };

        return new StorageTester(testerConfig);
    }
}