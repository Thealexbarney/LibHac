using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSystem;

namespace LibHac.FsSrv.FsCreator;

/// <summary>
/// Opens the partitions in NCAs as <see cref="IStorage"/>s.
/// </summary>
/// <remarks>Based on nnSdk 17.5.0 (FS 17.0.0)</remarks>
public class StorageOnNcaCreator17 : IStorageOnNcaCreator17
{
    private MemoryResource _memoryResource;
    private NcaCompressionConfiguration _compressionConfig;
    private IBufferManager _bufferManager;
    private NcaReaderInitializer _ncaReaderInitializer;
    private IHash256GeneratorFactorySelector _hashGeneratorFactorySelector;

    public StorageOnNcaCreator17(MemoryResource memoryResource, IBufferManager bufferManager,
        NcaReaderInitializer ncaReaderInitializer, in NcaCompressionConfiguration compressionConfig,
        IHash256GeneratorFactorySelector hashGeneratorFactorySelector)
    {
        _memoryResource = memoryResource;
        _compressionConfig = compressionConfig;
        _bufferManager = bufferManager;
        _ncaReaderInitializer = ncaReaderInitializer;
        _hashGeneratorFactorySelector = hashGeneratorFactorySelector;
    }

    public Result Create(ref SharedRef<IStorage> outStorage,
        ref SharedRef<IAsynchronousAccessSplitter> outStorageAccessSplitter, out NcaFsHeaderReader17 outHeaderReader,
        ref readonly SharedRef<NcaReader17> ncaReader, int fsIndex)
    {
        var ncaFsDriver = new NcaFileSystemDriver(in ncaReader, _memoryResource, _bufferManager, _hashGeneratorFactorySelector);

        using var storage = new SharedRef<IStorage>();
        using var storageAccessSplitter = new SharedRef<IAsynchronousAccessSplitter>();
        Result res = RomResultConverter.ConvertRomResult(ncaFsDriver.OpenStorage(ref storage.Ref,
            ref storageAccessSplitter.Ref, out outHeaderReader, fsIndex));
        if (res.IsFailure()) return res.Miss();

        using var resultConvertStorage = new SharedRef<RomResultConvertStorage>(new RomResultConvertStorage(in storage));

        outStorage.SetByMove(ref resultConvertStorage.Ref);
        outStorageAccessSplitter.SetByMove(ref storageAccessSplitter.Ref);

        return Result.Success;
    }

    public Result CreateWithPatch(ref SharedRef<IStorage> outStorage,
        ref SharedRef<IAsynchronousAccessSplitter> outStorageAccessSplitter, out NcaFsHeaderReader17 outHeaderReader,
        ref readonly SharedRef<NcaReader17> originalNcaReader, ref readonly SharedRef<NcaReader17> currentNcaReader,
        int fsIndex)
    {
        var ncaFsDriver = new NcaFileSystemDriver(in originalNcaReader, in currentNcaReader, _memoryResource,
            _bufferManager, _hashGeneratorFactorySelector);

        using var storage = new SharedRef<IStorage>();
        using var storageAccessSplitter = new SharedRef<IAsynchronousAccessSplitter>();
        Result res = RomResultConverter.ConvertRomResult(ncaFsDriver.OpenStorage(ref storage.Ref,
            ref storageAccessSplitter.Ref, out outHeaderReader, fsIndex));
        if (res.IsFailure()) return res.Miss();

        using var resultConvertStorage = new SharedRef<RomResultConvertStorage>(new RomResultConvertStorage(in storage));

        outStorage.SetByMove(ref resultConvertStorage.Ref);
        outStorageAccessSplitter.SetByMove(ref storageAccessSplitter.Ref);

        return Result.Success;
    }

    public Result CreateNcaReader(ref SharedRef<NcaReader17> outReader, ref readonly SharedRef<IStorage> baseStorage,
        ContentAttributes contentAttributes)
    {
        using var ncaReader = new SharedRef<NcaReader17>();

        Result res = RomResultConverter.ConvertRomResult(_ncaReaderInitializer(ref ncaReader.Ref, in baseStorage,
            in _compressionConfig, _hashGeneratorFactorySelector, contentAttributes));
        if (res.IsFailure()) return res.Miss();

        outReader.SetByMove(ref ncaReader.Ref);
        return Result.Success;
    }
}