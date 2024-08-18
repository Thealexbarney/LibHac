using LibHac.Diag;
using LibHac.Fs;

namespace LibHac.FsSrv.FsCreator;

/// <summary>
/// Converts internal RomFS <see cref="Result"/> values to external <see cref="Result"/>s. 
/// </summary>
/// <remarks>Based on nnSdk 17.5.0 (FS 17.0.0)</remarks>
public static class RomResultConverter
{
    private static Result ConvertCorruptedResult(Result result)
    {
        if (ResultFs.NcaCorrupted.Includes(result))
        {
            if (ResultFs.InvalidNcaFileSystemType.Includes(result))
                return ResultFs.InvalidRomNcaFileSystemType.LogConverted(result);

            if (ResultFs.InvalidNcaSignature.Includes(result))
                return ResultFs.InvalidRomNcaSignature.LogConverted(result);

            if (ResultFs.NcaHeaderSignature1VerificationFailed.Includes(result))
                return ResultFs.RomNcaHeaderSignature1VerificationFailed.LogConverted(result);

            if (ResultFs.NcaFsHeaderHashVerificationFailed.Includes(result))
                return ResultFs.RomNcaFsHeaderHashVerificationFailed.LogConverted(result);

            if (ResultFs.InvalidNcaKeyIndex.Includes(result))
                return ResultFs.InvalidRomNcaKeyIndex.LogConverted(result);

            if (ResultFs.InvalidNcaFsHeaderHashType.Includes(result))
                return ResultFs.InvalidRomNcaFsHeaderHashType.LogConverted(result);

            if (ResultFs.InvalidNcaFsHeaderEncryptionType.Includes(result))
                return ResultFs.InvalidRomNcaFsHeaderEncryptionType.LogConverted(result);

            if (ResultFs.InvalidNcaPatchInfoIndirectSize.Includes(result))
                return ResultFs.InvalidRomNcaPatchInfoIndirectSize.LogConverted(result);

            if (ResultFs.InvalidNcaPatchInfoAesCtrExSize.Includes(result))
                return ResultFs.InvalidRomNcaPatchInfoAesCtrExSize.LogConverted(result);

            if (ResultFs.InvalidNcaPatchInfoAesCtrExOffset.Includes(result))
                return ResultFs.InvalidRomNcaPatchInfoAesCtrExOffset.LogConverted(result);

            if (ResultFs.InvalidNcaId.Includes(result))
                return ResultFs.InvalidRomNcaId.LogConverted(result);

            if (ResultFs.InvalidNcaHeader.Includes(result))
                return ResultFs.InvalidRomNcaHeader.LogConverted(result);

            if (ResultFs.InvalidNcaFsHeader.Includes(result))
                return ResultFs.InvalidRomNcaFsHeader.LogConverted(result);

            if (ResultFs.InvalidNcaPatchInfoIndirectOffset.Includes(result))
                return ResultFs.InvalidRomNcaPatchInfoIndirectOffset.LogConverted(result);

            if (ResultFs.InvalidHierarchicalSha256BlockSize.Includes(result))
                return ResultFs.InvalidRomHierarchicalSha256BlockSize.LogConverted(result);

            if (ResultFs.InvalidHierarchicalSha256LayerCount.Includes(result))
                return ResultFs.InvalidRomHierarchicalSha256LayerCount.LogConverted(result);

            if (ResultFs.HierarchicalSha256BaseStorageTooLarge.Includes(result))
                return ResultFs.RomHierarchicalSha256BaseStorageTooLarge.LogConverted(result);

            if (ResultFs.HierarchicalSha256HashVerificationFailed.Includes(result))
                return ResultFs.RomHierarchicalSha256HashVerificationFailed.LogConverted(result);

            if (ResultFs.InvalidHierarchicalIntegrityVerificationLayerCount.Includes(result))
                return ResultFs.InvalidRomHierarchicalIntegrityVerificationLayerCount.LogConverted(result);

            if (ResultFs.NcaIndirectStorageOutOfRange.Includes(result))
                return ResultFs.RomNcaIndirectStorageOutOfRange.LogConverted(result);

            if (ResultFs.NcaInvalidCompressionInfo.Includes(result))
                return ResultFs.RomNcaInvalidCompressionInfo.LogConverted(result);

            Assert.SdkAssert(ResultFs.InvalidNcaHeader1SignatureKeyGeneration.Includes(result), $"Unknown Result 0x{result.Value:X8}");
            return result.Rethrow();
        }

        if (ResultFs.IntegrityVerificationStorageCorrupted.Includes(result))
        {
            if (ResultFs.IncorrectIntegrityVerificationMagicCode.Includes(result))
                return ResultFs.IncorrectRomIntegrityVerificationMagicCode.LogConverted(result);

            if (ResultFs.InvalidZeroHash.Includes(result))
                return ResultFs.InvalidRomZeroSignature.LogConverted(result);

            if (ResultFs.NonRealDataVerificationFailed.Includes(result))
                return ResultFs.RomNonRealDataVerificationFailed.LogConverted(result);

            if (ResultFs.ClearedRealDataVerificationFailed.Includes(result))
                return ResultFs.ClearedRomRealDataVerificationFailed.LogConverted(result);

            if (ResultFs.UnclearedRealDataVerificationFailed.Includes(result))
                return ResultFs.UnclearedRomRealDataVerificationFailed.LogConverted(result);

            Assert.SdkAssert(false, $"Unknown Result 0x{result.Value:X8}");
            return result.Rethrow();
        }

        if (ResultFs.PartitionFileSystemCorrupted.Includes(result))
        {
            if (ResultFs.InvalidSha256PartitionHashTarget.Includes(result))
                return ResultFs.InvalidRomSha256PartitionHashTarget.LogConverted(result);

            if (ResultFs.Sha256PartitionHashVerificationFailed.Includes(result))
                return ResultFs.RomSha256PartitionHashVerificationFailed.LogConverted(result);

            if (ResultFs.PartitionSignatureVerificationFailed.Includes(result))
                return ResultFs.RomPartitionSignatureVerificationFailed.LogConverted(result);

            if (ResultFs.Sha256PartitionSignatureVerificationFailed.Includes(result))
                return ResultFs.RomSha256PartitionSignatureVerificationFailed.LogConverted(result);

            if (ResultFs.InvalidPartitionEntryOffset.Includes(result))
                return ResultFs.InvalidRomPartitionEntryOffset.LogConverted(result);

            if (ResultFs.InvalidSha256PartitionMetaDataSize.Includes(result))
                return ResultFs.InvalidRomSha256PartitionMetaDataSize.LogConverted(result);

            Assert.SdkAssert(false, $"Unknown Result 0x{result.Value:X8}");
            return result.Rethrow();
        }

        if (ResultFs.HostFileSystemCorrupted.Includes(result))
        {
            if (ResultFs.HostEntryCorrupted.Includes(result))
                return ResultFs.RomHostEntryCorrupted.LogConverted(result);

            if (ResultFs.HostFileDataCorrupted.Includes(result))
                return ResultFs.RomHostFileDataCorrupted.LogConverted(result);

            if (ResultFs.HostFileCorrupted.Includes(result))
                return ResultFs.RomHostFileCorrupted.LogConverted(result);

            if (ResultFs.InvalidHostHandle.Includes(result))
                return ResultFs.InvalidRomHostHandle.LogConverted(result);

            Assert.SdkAssert(false, $"Unknown Result 0x{result.Value:X8}");
            return result.Rethrow();
        }

        if (result.IsSuccess())
            return Result.Success;

        return result.Miss();
    }

    public static Result ConvertRomResult(Result result)
    {
        if (result.IsSuccess())
            return Result.Success;

        if (ResultFs.UnsupportedVersion.Includes(result))
            return ResultFs.UnsupportedRomVersion.LogConverted(result);

        if (ResultFs.NcaCorrupted.Includes(result) ||
            ResultFs.IntegrityVerificationStorageCorrupted.Includes(result) ||
            ResultFs.BuiltInStorageCorrupted.Includes(result) ||
            ResultFs.PartitionFileSystemCorrupted.Includes(result) ||
            ResultFs.HostFileSystemCorrupted.Includes(result))
        {
            return ConvertCorruptedResult(result);
        }

        if (ResultFs.FatFileSystemCorrupted.Includes(result))
            return result.Miss();

        if (ResultFs.NotFound.Includes(result))
            return ResultFs.PathNotFound.LogConverted(result);

        if (ResultFs.FileNotFound.Includes(result) ||
            ResultFs.IncompatiblePath.Includes(result))
        {
            return ResultFs.PathNotFound.LogConverted(result);
        }

        return result;
    }
}