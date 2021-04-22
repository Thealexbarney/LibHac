using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.Loader
{
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
            Result rc = GetNpdmFromBuffer(out _, npdmBuffer);
            if (rc.IsFailure()) return rc;

            npdmBuffer.CopyTo(_npdmBuffer);
            _isValid = true;
            return Result.Success;
        }

        public Result LoadFromFile(HorizonClient hos, FileHandle file)
        {
            _isValid = false;

            // Get file size
            Result rc = hos.Fs.GetFileSize(out long npdmSize, file);
            if (rc.IsFailure()) return rc;

            if (npdmSize > MetaCacheBufferSize)
                return ResultLoader.TooLargeMeta.Log();

            // Read data into cache buffer
            rc = hos.Fs.ReadFile(file, 0, _npdmBuffer.AsSpan(0, (int)npdmSize));
            if (rc.IsFailure()) return rc;

            // Validate the meta
            rc = GetNpdmFromBuffer(out _, _npdmBuffer);
            if (rc.IsFailure()) return rc;

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

            npdm = GetNpdmFromBufferUnsafe(ref MemoryMarshal.GetArrayDataReference(_npdmBuffer));
            return Result.Success;
        }

        public static Result GetNpdmFromBuffer(out Npdm npdm, ReadOnlySpan<byte> npdmBuffer)
        {
            npdm = default;

            int npdmSize = npdmBuffer.Length;

            if (npdmSize > MetaCacheBufferSize)
                return ResultLoader.TooLargeMeta.Log();

            Result rc = ValidateMeta(npdmBuffer);
            if (rc.IsFailure()) return rc;

            ref readonly Meta meta = ref Unsafe.As<byte, Meta>(ref MemoryMarshal.GetReference(npdmBuffer));

            ReadOnlySpan<byte> acidBuffer = npdmBuffer.Slice(meta.AcidOffset, meta.AcidSize);
            ReadOnlySpan<byte> aciBuffer = npdmBuffer.Slice(meta.AciOffset, meta.AciSize);

            ref readonly AcidHeaderData acid = ref Unsafe.As<byte, AcidHeaderData>(ref MemoryMarshal.GetReference(acidBuffer));
            ref readonly AciHeader aci = ref Unsafe.As<byte, AciHeader>(ref MemoryMarshal.GetReference(aciBuffer));

            rc = ValidateAcid(acidBuffer);
            if (rc.IsFailure()) return rc;

            rc = ValidateAci(aciBuffer);
            if (rc.IsFailure()) return rc;

            // Set Npdm members.
            npdm.Meta = new ReadOnlyRef<Meta>(in meta);
            npdm.Acid = new ReadOnlyRef<AcidHeaderData>(in acid);
            npdm.Aci = new ReadOnlyRef<AciHeader>(in aci);

            npdm.FsAccessControlDescriptor = acidBuffer.Slice(acid.FsAccessControlOffset, acid.FsAccessControlSize);
            npdm.ServiceAccessControlDescriptor = acidBuffer.Slice(acid.ServiceAccessControlOffset, acid.ServiceAccessControlSize);
            npdm.KernelCapabilityDescriptor = acidBuffer.Slice(acid.KernelCapabilityOffset, acid.KernelCapabilitySize);

            npdm.FsAccessControlData = aciBuffer.Slice(aci.FsAccessControlOffset, aci.FsAccessControlSize);
            npdm.ServiceAccessControlData = aciBuffer.Slice(aci.ServiceAccessControlOffset, aci.ServiceAccessControlSize);
            npdm.KernelCapabilityData = aciBuffer.Slice(aci.KernelCapabilityOffset, aci.KernelCapabilitySize);

            return Result.Success;
        }

        private static Npdm GetNpdmFromBufferUnsafe(ref byte npdmBuffer)
        {
            var npdm = new Npdm();

            ref Meta meta = ref Unsafe.As<byte, Meta>(ref npdmBuffer);
            ref AcidHeaderData acid = ref Unsafe.As<byte, AcidHeaderData>(ref Unsafe.Add(ref npdmBuffer, meta.AcidOffset));
            ref AciHeader aci = ref Unsafe.As<byte, AciHeader>(ref Unsafe.Add(ref npdmBuffer, meta.AciOffset));

            // Set Npdm members.
            npdm.Meta = new ReadOnlyRef<Meta>(in meta);
            npdm.Acid = new ReadOnlyRef<AcidHeaderData>(in acid);
            npdm.Aci = new ReadOnlyRef<AciHeader>(in aci);

            npdm.FsAccessControlDescriptor = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref Unsafe.As<AcidHeaderData, byte>(ref acid), acid.FsAccessControlOffset), acid.FsAccessControlSize);
            npdm.ServiceAccessControlDescriptor = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref Unsafe.As<AcidHeaderData, byte>(ref acid), acid.ServiceAccessControlOffset), acid.ServiceAccessControlSize);
            npdm.KernelCapabilityDescriptor = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref Unsafe.As<AcidHeaderData, byte>(ref acid), acid.KernelCapabilityOffset), acid.KernelCapabilitySize);

            npdm.FsAccessControlData = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref Unsafe.As<AciHeader, byte>(ref aci), aci.FsAccessControlOffset), aci.FsAccessControlSize);
            npdm.ServiceAccessControlData = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref Unsafe.As<AciHeader, byte>(ref aci), aci.ServiceAccessControlOffset), aci.ServiceAccessControlSize);
            npdm.KernelCapabilityData = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref Unsafe.As<AciHeader, byte>(ref aci), aci.KernelCapabilityOffset), aci.KernelCapabilitySize);

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
            Result rc = ValidateSubregion(Unsafe.SizeOf<Meta>(), metaBuffer.Length, meta.AcidOffset,
                Unsafe.SizeOf<AcidHeaderData>());
            if (rc.IsFailure()) return rc;

            // Validate Aci extends.
            rc = ValidateSubregion(Unsafe.SizeOf<Meta>(), metaBuffer.Length, meta.AciOffset, Unsafe.SizeOf<AciHeader>());
            if (rc.IsFailure()) return rc;

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
            Result rc = ValidateSubregion(Unsafe.SizeOf<AcidHeaderData>(), acidBuffer.Length, acid.FsAccessControlOffset,
                acid.FsAccessControlSize);
            if (rc.IsFailure()) return rc;

            rc = ValidateSubregion(Unsafe.SizeOf<AcidHeaderData>(), acidBuffer.Length, acid.ServiceAccessControlOffset,
                acid.ServiceAccessControlSize);
            if (rc.IsFailure()) return rc;

            rc = ValidateSubregion(Unsafe.SizeOf<AcidHeaderData>(), acidBuffer.Length, acid.KernelCapabilityOffset,
                acid.KernelCapabilitySize);
            if (rc.IsFailure()) return rc;

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
            Result rc = ValidateSubregion(Unsafe.SizeOf<AciHeader>(), aciBuffer.Length, aci.FsAccessControlOffset,
                aci.FsAccessControlSize);
            if (rc.IsFailure()) return rc;

            rc = ValidateSubregion(Unsafe.SizeOf<AciHeader>(), aciBuffer.Length, aci.ServiceAccessControlOffset,
                aci.ServiceAccessControlSize);
            if (rc.IsFailure()) return rc;

            rc = ValidateSubregion(Unsafe.SizeOf<AciHeader>(), aciBuffer.Length, aci.KernelCapabilityOffset,
                aci.KernelCapabilitySize);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }
    }
}
