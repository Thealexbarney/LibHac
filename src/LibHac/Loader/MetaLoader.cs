using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.Loader;

public class MetaLoader
{
    private const int MetaCacheBufferSize = 0x8000;

    private readonly byte[] _npdmBuffer;
    private bool _isValid;

    public MetaLoader()
    {
        _npdmBuffer = new byte[MetaCacheBufferSize];
    }

    public Result Load(ReadOnlySpan<byte> npdmBuffer)
    {
        _isValid = false;

        // Validate the meta
        Result res = GetNpdmFromBuffer(out _, npdmBuffer);
        if (res.IsFailure()) return res.Miss();

        npdmBuffer.CopyTo(_npdmBuffer);
        _isValid = true;
        return Result.Success;
    }

    public Result LoadFromFile(HorizonClient hos, FileHandle file)
    {
        _isValid = false;

        // Get file size
        Result res = hos.Fs.GetFileSize(out long npdmSize, file);
        if (res.IsFailure()) return res.Miss();

        if (npdmSize > MetaCacheBufferSize)
            return ResultLoader.TooLargeMeta.Log();

        // Read data into cache buffer
        res = hos.Fs.ReadFile(file, 0, _npdmBuffer.AsSpan(0, (int)npdmSize));
        if (res.IsFailure()) return res.Miss();

        // Validate the meta
        res = GetNpdmFromBuffer(out _, _npdmBuffer);
        if (res.IsFailure()) return res.Miss();

        _isValid = true;
        return Result.Success;
    }

    public Result GetNpdm(out Npdm npdm)
    {
        Assert.SdkRequires(_isValid);
        Assert.SdkRequiresEqual(MetaCacheBufferSize, _npdmBuffer.Length);

        if (!_isValid)
        {
            npdm = default;
            return ResultLoader.InvalidMeta.Log();
        }

        npdm = GetNpdmFromBufferUnsafe(_npdmBuffer);
        return Result.Success;
    }

    public static Result GetNpdmFromBuffer(out Npdm npdm, ReadOnlySpan<byte> npdmBuffer)
    {
        npdm = default;

        int npdmSize = npdmBuffer.Length;

        if (npdmSize > MetaCacheBufferSize)
            return ResultLoader.TooLargeMeta.Log();

        Result res = ValidateMeta(npdmBuffer);
        if (res.IsFailure()) return res.Miss();

        ref readonly Meta meta = ref Unsafe.As<byte, Meta>(ref MemoryMarshal.GetReference(npdmBuffer));

        ReadOnlySpan<byte> acidBuffer = npdmBuffer.Slice(meta.AcidOffset, meta.AcidSize);
        ReadOnlySpan<byte> aciBuffer = npdmBuffer.Slice(meta.AciOffset, meta.AciSize);

        ref readonly AcidHeaderData acid = ref Unsafe.As<byte, AcidHeaderData>(ref MemoryMarshal.GetReference(acidBuffer));
        ref readonly AciHeader aci = ref Unsafe.As<byte, AciHeader>(ref MemoryMarshal.GetReference(aciBuffer));

        res = ValidateAcid(acidBuffer);
        if (res.IsFailure()) return res.Miss();

        res = ValidateAci(aciBuffer);
        if (res.IsFailure()) return res.Miss();

        // Set Npdm members.
        npdm.Meta = ref meta;
        npdm.Acid = ref acid;
        npdm.Aci = ref aci;

        npdm.FsAccessControlDescriptor = acidBuffer.Slice(acid.FsAccessControlOffset, acid.FsAccessControlSize);
        npdm.ServiceAccessControlDescriptor = acidBuffer.Slice(acid.ServiceAccessControlOffset, acid.ServiceAccessControlSize);
        npdm.KernelCapabilityDescriptor = acidBuffer.Slice(acid.KernelCapabilityOffset, acid.KernelCapabilitySize);

        npdm.FsAccessControlData = aciBuffer.Slice(aci.FsAccessControlOffset, aci.FsAccessControlSize);
        npdm.ServiceAccessControlData = aciBuffer.Slice(aci.ServiceAccessControlOffset, aci.ServiceAccessControlSize);
        npdm.KernelCapabilityData = aciBuffer.Slice(aci.KernelCapabilityOffset, aci.KernelCapabilitySize);

        return Result.Success;
    }

    private static Npdm GetNpdmFromBufferUnsafe(ReadOnlySpan<byte> npdmSpan)
    {
        ref byte npdmBuffer = ref MemoryMarshal.GetReference(npdmSpan);

        var npdm = new Npdm();

        ref Meta meta = ref Unsafe.As<byte, Meta>(ref npdmBuffer);
        ref AcidHeaderData acid = ref Unsafe.As<byte, AcidHeaderData>(ref Unsafe.Add(ref npdmBuffer, meta.AcidOffset));
        ref AciHeader aci = ref Unsafe.As<byte, AciHeader>(ref Unsafe.Add(ref npdmBuffer, meta.AciOffset));

        // Set Npdm members.
        npdm.Meta = ref meta;
        npdm.Acid = ref acid;
        npdm.Aci = ref aci;

        npdm.FsAccessControlDescriptor = SpanHelpers.CreateReadOnlySpan(in Unsafe.Add(ref Unsafe.As<AcidHeaderData, byte>(ref acid), acid.FsAccessControlOffset), acid.FsAccessControlSize);
        npdm.ServiceAccessControlDescriptor = SpanHelpers.CreateReadOnlySpan(in Unsafe.Add(ref Unsafe.As<AcidHeaderData, byte>(ref acid), acid.ServiceAccessControlOffset), acid.ServiceAccessControlSize);
        npdm.KernelCapabilityDescriptor = SpanHelpers.CreateReadOnlySpan(in Unsafe.Add(ref Unsafe.As<AcidHeaderData, byte>(ref acid), acid.KernelCapabilityOffset), acid.KernelCapabilitySize);

        npdm.FsAccessControlData = SpanHelpers.CreateReadOnlySpan(in Unsafe.Add(ref Unsafe.As<AciHeader, byte>(ref aci), aci.FsAccessControlOffset), aci.FsAccessControlSize);
        npdm.ServiceAccessControlData = SpanHelpers.CreateReadOnlySpan(in Unsafe.Add(ref Unsafe.As<AciHeader, byte>(ref aci), aci.ServiceAccessControlOffset), aci.ServiceAccessControlSize);
        npdm.KernelCapabilityData = SpanHelpers.CreateReadOnlySpan(in Unsafe.Add(ref Unsafe.As<AciHeader, byte>(ref aci), aci.KernelCapabilityOffset), aci.KernelCapabilitySize);

        return npdm;
    }

    private static Result ValidateSubregion(int allowedStart, int allowedEnd, int start, int size, int minSize = 0)
    {
        if (size < minSize) return ResultLoader.InvalidMeta.Log();
        if (allowedStart > start) return ResultLoader.InvalidMeta.Log();
        if (start > allowedEnd) return ResultLoader.InvalidMeta.Log();
        if (start + size > allowedEnd) return ResultLoader.InvalidMeta.Log();
        return Result.Success;
    }

    private static Result ValidateMeta(ReadOnlySpan<byte> metaBuffer)
    {
        // Validate the buffer is large enough
        if (metaBuffer.Length < Unsafe.SizeOf<Meta>())
            return ResultLoader.InvalidMeta.Log();

        ref Meta meta = ref Unsafe.As<byte, Meta>(ref MemoryMarshal.GetReference(metaBuffer));

        // Validate magic.
        if (meta.Magic != Meta.MagicValue)
            return ResultLoader.InvalidMeta.Log();

        // Validate flags.
        uint invalidFlagsMask = ~0x3Fu;

        if ((meta.Flags & invalidFlagsMask) != 0)
            return ResultLoader.InvalidMeta.Log();

        // Validate Acid extents.
        Result res = ValidateSubregion(Unsafe.SizeOf<Meta>(), metaBuffer.Length, meta.AcidOffset,
            Unsafe.SizeOf<AcidHeaderData>());
        if (res.IsFailure()) return res.Miss();

        // Validate Aci extends.
        res = ValidateSubregion(Unsafe.SizeOf<Meta>(), metaBuffer.Length, meta.AciOffset, Unsafe.SizeOf<AciHeader>());
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    private static Result ValidateAcid(ReadOnlySpan<byte> acidBuffer)
    {
        // Validate the buffer is large enough
        if (acidBuffer.Length < Unsafe.SizeOf<AcidHeaderData>())
            return ResultLoader.InvalidMeta.Log();

        ref AcidHeaderData acid = ref Unsafe.As<byte, AcidHeaderData>(ref MemoryMarshal.GetReference(acidBuffer));

        // Validate magic.
        if (acid.Magic != AcidHeaderData.MagicValue)
            return ResultLoader.InvalidMeta.Log();

        // Validate Fac, Sac, Kac.
        Result res = ValidateSubregion(Unsafe.SizeOf<AcidHeaderData>(), acidBuffer.Length, acid.FsAccessControlOffset,
            acid.FsAccessControlSize);
        if (res.IsFailure()) return res.Miss();

        res = ValidateSubregion(Unsafe.SizeOf<AcidHeaderData>(), acidBuffer.Length, acid.ServiceAccessControlOffset,
            acid.ServiceAccessControlSize);
        if (res.IsFailure()) return res.Miss();

        res = ValidateSubregion(Unsafe.SizeOf<AcidHeaderData>(), acidBuffer.Length, acid.KernelCapabilityOffset,
            acid.KernelCapabilitySize);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    private static Result ValidateAci(ReadOnlySpan<byte> aciBuffer)
    {
        // Validate the buffer is large enough
        if (aciBuffer.Length < Unsafe.SizeOf<AciHeader>())
            return ResultLoader.InvalidMeta.Log();

        ref AciHeader aci = ref Unsafe.As<byte, AciHeader>(ref MemoryMarshal.GetReference(aciBuffer));

        // Validate magic.
        if (aci.Magic != AciHeader.MagicValue)
            return ResultLoader.InvalidMeta.Log();

        // Validate Fac, Sac, Kac.
        Result res = ValidateSubregion(Unsafe.SizeOf<AciHeader>(), aciBuffer.Length, aci.FsAccessControlOffset,
            aci.FsAccessControlSize);
        if (res.IsFailure()) return res.Miss();

        res = ValidateSubregion(Unsafe.SizeOf<AciHeader>(), aciBuffer.Length, aci.ServiceAccessControlOffset,
            aci.ServiceAccessControlSize);
        if (res.IsFailure()) return res.Miss();

        res = ValidateSubregion(Unsafe.SizeOf<AciHeader>(), aciBuffer.Length, aci.KernelCapabilityOffset,
            aci.KernelCapabilitySize);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }
}