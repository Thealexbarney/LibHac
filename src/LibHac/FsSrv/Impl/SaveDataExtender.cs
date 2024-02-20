using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.FsSystem.Save;
using LibHac.Util;

namespace LibHac.FsSrv.Impl;

/// <summary>
/// Extends a save data image to a larger size. Keeps track of the extension progress by writing information about
/// the extension to an "extension context" storage.  
/// </summary>
/// <remarks>Based on nnSdk 17.5.0 (FS 17.0.0)</remarks>
public class SaveDataExtender
{
    private const uint MagicCode = 0x43545845; // EXTC
    private const uint Version1 = 0x10000;
    private const uint Version2 = 0x20000;

    private enum State
    {
        Initial = 1,
        Extended = 2,
        Committed = 3
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Context
    {
        public uint Magic;
        public uint Version;
        public State State;
        public long AvailableSize;
        public long JournalSize;
        public long BlockSize;
        public long ExtendedSaveDataSize;
    }

    private IStorage _contextStorage;
    private Context _context;

    public static long QueryContextSize() => Unsafe.SizeOf<Context>();

    public SaveDataExtender()
    {
        _contextStorage = null;
    }

    public long GetLogSize() => JournalIntegritySaveDataFileSystemDriver.QueryExpandLogSize(_context.BlockSize,
        GetJournalBlockCount(), GetAvailableBlockCount());

    public long GetAvailableSize() => _context.AvailableSize;
    public long GetJournalSize() => _context.JournalSize;
    public long GetExtendedSaveDataSize() => _context.ExtendedSaveDataSize;

    private uint GetAvailableBlockCount() => (uint)BitUtil.DivideUp(_context.AvailableSize, _context.BlockSize);
    private uint GetJournalBlockCount() => (uint)BitUtil.DivideUp(_context.JournalSize, _context.BlockSize);

    public Result InitializeContext(in JournalIntegritySaveDataParameters param, long sizeAvailable, long sizeReserved)
    {
        _context.Magic = MagicCode;
        _context.Version = Version2;
        _context.State = State.Initial;
        _context.AvailableSize = sizeAvailable;
        _context.JournalSize = sizeReserved;
        _context.BlockSize = param.BlockSize;

        Result res = JournalIntegritySaveDataFileSystemDriver.QueryTotalSize(out _context.ExtendedSaveDataSize,
            param.BlockSize, GetAvailableBlockCount(), GetJournalBlockCount(), param.CountExpandMax, param.Version);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result WriteContext(IStorage contextStorage)
    {
        Result res = contextStorage.Write(0, SpanHelpers.AsReadOnlyByteSpan(in _context));
        if (res.IsFailure()) return res.Miss();

        res = contextStorage.Flush();
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result ReadContext(IStorage contextStorage)
    {
        Assert.SdkRequiresNull(_contextStorage);
        Assert.SdkRequiresNotNull(contextStorage);

        Result res = contextStorage.Read(0, SpanHelpers.AsByteSpan(ref _context));
        if (res.IsFailure()) return res.Miss();

        if (_context.Magic != MagicCode)
            return ResultFs.IncorrectSaveDataExtensionContextMagicCode.Log();

        if (_context.Version == Version1)
        {
            UpdateContextV1ToV2(ref _context);
        }

        if (_context.Version != Version2)
            return ResultFs.UnsupportedSaveDataVersion.Log();

        State state = _context.State;
        if (state != State.Initial && state != State.Extended && state != State.Committed)
            return ResultFs.InvalidSaveDataExtensionContextState.Log();

        if (_context.BlockSize <= 0)
            return ResultFs.InvalidSaveDataExtensionContextParameter.Log();

        if (_context.ExtendedSaveDataSize <= 0)
            return ResultFs.InvalidSaveDataExtensionContextParameter.Log();

        _contextStorage = contextStorage;
        return Result.Success;
    }

    public Result Extend(in ValueSubStorage saveDataStorage, in ValueSubStorage logStorage, IBufferManager bufferManager,
        IMacGenerator macGenerator, IHash256GeneratorFactorySelector hashGeneratorFactorySelector, uint minimumVersion)
    {
        Assert.SdkRequiresNotNull(_contextStorage);

        if (_context.State == State.Initial)
        {
            Result res = JournalIntegritySaveDataFileSystemDriver.OperateExpand(in saveDataStorage, in logStorage,
                _context.BlockSize, GetAvailableBlockCount(), GetJournalBlockCount(), bufferManager, macGenerator,
                hashGeneratorFactorySelector, minimumVersion);
            if (res.IsFailure()) return res.Miss();

            _context.State = State.Extended;
            res = WriteContext(_contextStorage);
            if (res.IsFailure()) return res.Miss();
        }

        if (_context.State == State.Extended)
        {
            Result res = JournalIntegritySaveDataFileSystemDriver.CommitExpand(in saveDataStorage, in logStorage,
                _context.BlockSize, bufferManager);
            if (res.IsFailure()) return res.Miss();

            _context.State = State.Committed;
            res = WriteContext(_contextStorage);
            if (res.IsFailure()) return res.Miss();
        }

        if (_context.State != State.Committed)
            return ResultFs.BadState.Log();

        return Result.Success;
    }

    private void UpdateContextV1ToV2(ref Context context)
    {
        Assert.SdkAssert(context.Version == Version1);

        context.AvailableSize *= context.BlockSize;
        context.JournalSize *= context.BlockSize;
        context.Version = Version2;
    }
}