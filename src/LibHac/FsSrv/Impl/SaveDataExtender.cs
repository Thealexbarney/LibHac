// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
using System;
using System.Runtime.CompilerServices;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.FsSystem.Save;

namespace LibHac.FsSrv.Impl;

public class SaveDataExtender
{
    private enum State
    {
        Initial = 1,
        Extended = 2,
        Committed = 3
    }

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
        throw new NotImplementedException();
    }

    public Result InitializeContext(in JournalIntegritySaveDataParameters param, long sizeAvailable, long sizeReserved)
    {
        throw new NotImplementedException();
    }

    public Result WriteContext(IStorage contextStorage)
    {
        throw new NotImplementedException();
    }

    public Result ReadContext(IStorage contextStorage)
    {
        throw new NotImplementedException();
    }

    public long GetLogSize()
    {
        throw new NotImplementedException();
    }

    public long GetAvailableSize()
    {
        throw new NotImplementedException();
    }

    public long GetJournalSize()
    {
        throw new NotImplementedException();
    }

    public long GetExtendedSaveDataSize()
    {
        throw new NotImplementedException();
    }

    private uint GetAvailableBlockCount()
    {
        throw new NotImplementedException();
    }

    private uint GetJournalBlockCount()
    {
        throw new NotImplementedException();
    }

    public Result Extend(in ValueSubStorage saveDataStorage, in ValueSubStorage logStorage, IBufferManager bufferManager,
        IMacGenerator macGenerator, IHash256GeneratorFactorySelector hashGeneratorFactorySelector, uint minimumVersion)
    {
        throw new NotImplementedException();
    }

    private void UpdateContextV1ToV2(ref Context context)
    {
        throw new NotImplementedException();
    }
}