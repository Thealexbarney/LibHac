using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using Buffer = LibHac.Mem.Buffer;
using RomFsFileSystem = LibHac.FsSystem.RomFsFileSystem;

namespace LibHac.FsSrv.FsCreator;

/// <summary>
/// Extends <see cref="RomFsFileSystem"/> by allocating a cache buffer that is then deallocated upon disposal.
/// </summary>
/// <remarks>Based on nnSdk 18.3.0 (FS 18.0.0)</remarks>
file class RomFileSystemWithBuffer : RomFsFileSystem
{
    private const int MaxBufferSize = 1024 * 128;

    private Buffer _metaCacheBuffer;
    private MemoryResource _allocator;

    public RomFileSystemWithBuffer(MemoryResource allocator)
    {
        _allocator = allocator;
    }

    public override void Dispose()
    {
        if (!_metaCacheBuffer.IsNull)
            _allocator.Deallocate(ref _metaCacheBuffer);

        base.Dispose();
    }

    public Result Initialize(ref readonly SharedRef<IStorage> baseStorage)
    {
        Result res = GetRequiredWorkingMemorySize(out long bufferSize, baseStorage.Get);
        if (res.IsFailure() || bufferSize == 0 || bufferSize >= MaxBufferSize)
        {
            return Initialize(in baseStorage, Buffer.Empty, useCache: false).Ret();
        }

        _metaCacheBuffer = _allocator.Allocate(bufferSize);
        if (_metaCacheBuffer.IsNull)
        {
            return Initialize(in baseStorage, Buffer.Empty, useCache: false).Ret();
        }

        return Initialize(in baseStorage, _metaCacheBuffer, useCache: true).Ret();
    }
}

/// <summary>
/// Takes a <see cref="IStorage"/> containing a RomFs and opens it as an <see cref="IFileSystem"/>
/// </summary>
/// <remarks>Based on nnSdk 18.3.0 (FS 18.0.0)</remarks>
public class RomFileSystemCreator : IRomFileSystemCreator
{
    private MemoryResource _allocator;

    public RomFileSystemCreator(MemoryResource allocator)
    {
        _allocator = allocator;
    }

    public void Dispose() { }

    public Result Create(ref SharedRef<IFileSystem> outFileSystem, ref readonly SharedRef<IStorage> romFsStorage)
    {
        using var fs = new SharedRef<RomFileSystemWithBuffer>(new RomFileSystemWithBuffer(_allocator));
        if (!fs.HasValue) return ResultFs.AllocationMemoryFailedInRomFileSystemCreatorA.Log();

        Result res = fs.Get.Initialize(in romFsStorage);
        if (res.IsFailure()) return res.Miss();

        outFileSystem.SetByMove(ref fs.Ref);
        return Result.Success;
    }
}