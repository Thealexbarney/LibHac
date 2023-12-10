using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;

namespace LibHac.Kernel;

public class InitialProcessBinaryReader : IDisposable
{
    internal const uint ExpectedMagic = 0x31494E49; // INI1
    private const int MaxProcessCount = 80;

    private SharedRef<IStorage> _storage;
    private IniHeader _header;
    private (int offset, int size)[] _offsets;

    public ref readonly IniHeader Header => ref _header;
    public int ProcessCount => _header.ProcessCount;

    public void Dispose()
    {
        _storage.Destroy();
    }

    public Result Initialize(ref readonly SharedRef<IStorage> binaryStorage)
    {
        if (!binaryStorage.HasValue)
            return ResultLibHac.NullArgument.Log();

        // Verify there's enough data to read the header
        Result res = binaryStorage.Get.GetSize(out long iniSize);
        if (res.IsFailure()) return res.Miss();

        if (iniSize < Unsafe.SizeOf<IniHeader>())
            return ResultLibHac.InvalidIniFileSize.Log();

        // Read the INI file header and validate some of its values.
        res = binaryStorage.Get.Read(0, SpanHelpers.AsByteSpan(ref _header));
        if (res.IsFailure()) return res.Miss();

        if (_header.Magic != ExpectedMagic)
            return ResultLibHac.InvalidIniMagic.Log();

        if ((uint)_header.ProcessCount > MaxProcessCount)
            return ResultLibHac.InvalidIniProcessCount.Log();

        // There's no metadata with the offsets of each KIP; they're all stored sequentially in the file.
        // Read the size of each KIP to get their offsets.
        res = GetKipOffsets(out _offsets, in binaryStorage, _header.ProcessCount);
        if (res.IsFailure()) return res.Miss();

        _storage.SetByCopy(in binaryStorage);
        return Result.Success;
    }

    public Result OpenKipStorage(ref UniqueRef<IStorage> outStorage, int index)
    {
        if ((uint)index >= _header.ProcessCount)
            return ResultLibHac.ArgumentOutOfRange.Log();

        (int offset, int size) range = _offsets[index];
        outStorage.Reset(new SubStorage(in _storage, range.offset, range.size));
        return Result.Success;
    }

    private static Result GetKipOffsets(out (int offset, int size)[] kipOffsets,
        ref readonly SharedRef<IStorage> iniStorage, int processCount)
    {
        Assert.SdkRequiresLessEqual(processCount, MaxProcessCount);

        UnsafeHelpers.SkipParamInit(out kipOffsets);

        Result res = iniStorage.Get.GetSize(out long iniStorageSize);
        if (res.IsFailure()) return res.Miss();

        var offsets = new (int offset, int size)[processCount];
        int offset = Unsafe.SizeOf<IniHeader>();
        using var kipReader = new KipReader();

        for (int i = 0; i < processCount; i++)
        {
            using var kipStorage =
                new SharedRef<IStorage>(new SubStorage(in iniStorage, offset, iniStorageSize - offset));

            res = kipReader.Initialize(in kipStorage);
            if (res.IsFailure()) return res.Miss();

            int kipSize = kipReader.GetFileSize();
            offsets[i] = (offset, kipSize);
            offset += kipSize;
        }

        kipOffsets = offsets;
        return Result.Success;
    }

    public struct IniHeader
    {
        public uint Magic;
        public int Size;
        public int ProcessCount;
        public uint Reserved;
    }
}