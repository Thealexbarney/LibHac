using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Diag;
using LibHac.Fs.Impl;
using LibHac.Fs.Shim;
using LibHac.FsSrv.Sf;
using LibHac.Sf;
using LibHac.Util;
using static LibHac.Fs.SaveData;

// ReSharper disable once CheckNamespace
namespace LibHac.Fs
{
    /// <summary>
    /// An adapter for interacting with an <see cref="FsSrv.Sf.ISaveDataTransferManagerWithDivision"/>
    /// IPC service object.
    /// </summary>
    /// <remarks>Based on nnSdk 14.3.0</remarks>
    public class SaveDataTransferManagerVersion2 : IDisposable
    {
        private SharedRef<ISaveDataTransferManagerWithDivision> _baseInterface;

        // LibHac addition
        private FileSystemClient _fsClient;

        public struct Challenge
        {
            public Array16<byte> Value;
        }

        public struct SaveDataTag
        {
            public Array64<byte> Value;
        }

        public struct KeySeedPackage
        {
            public Array512<byte> Value;
        }

        public SaveDataTransferManagerVersion2(FileSystemClient fs)
        {
            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

            Result res = fileSystemProxy.Get.OpenSaveDataTransferManagerVersion2(ref _baseInterface);
            fs.Impl.LogResultErrorMessage(res);
            Abort.DoAbortUnless(res.IsSuccess());

            _fsClient = fs;
        }

        public void Dispose()
        {
            _baseInterface.Destroy();
        }

        public Result GetChallenge(out Challenge outChallenge)
        {
            UnsafeHelpers.SkipParamInit(out outChallenge);

            Result res = _baseInterface.Get.GetChallenge(OutBuffer.FromStruct(ref outChallenge));
            _fsClient.Impl.LogResultErrorMessage(res);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }

        public Result SetKeySeedPackage(in KeySeedPackage keySeedPackage)
        {
            Result res = _baseInterface.Get.SetKeySeedPackage(InBuffer.FromStruct(in keySeedPackage));
            _fsClient.Impl.LogResultErrorMessage(res);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }

        public Result OpenSaveDataFullExporter(ref UniqueRef<ISaveDataDivisionExporter> outExporter,
            SaveDataSpaceId spaceId, ulong saveDataId)
        {
            using var exporterInterface = new SharedRef<FsSrv.Sf.ISaveDataDivisionExporter>();

            Result res = _baseInterface.Get.OpenSaveDataExporter(ref exporterInterface.Ref(), spaceId, saveDataId);
            _fsClient.Impl.LogResultErrorMessage(res);
            if (res.IsFailure()) return res.Miss();

            outExporter.Reset(new SaveDataExporterVersion2(_fsClient, ref exporterInterface.Ref()));
            return Result.Success;
        }

        public Result OpenSaveDataDiffExporter(ref UniqueRef<ISaveDataDivisionExporter> outExporter,
            in InitialDataVersion2 initialData, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            using var exporterInterface = new SharedRef<FsSrv.Sf.ISaveDataDivisionExporter>();

            Result res = _baseInterface.Get.OpenSaveDataExporterForDiffExport(ref exporterInterface.Ref(),
                InBuffer.FromStruct(in initialData), spaceId, saveDataId);

            _fsClient.Impl.LogResultErrorMessage(res);
            if (res.IsFailure()) return res.Miss();

            outExporter.Reset(new SaveDataExporterVersion2(_fsClient, ref exporterInterface.Ref()));
            return Result.Success;
        }

        public Result OpenSaveDataExporterByContext(ref UniqueRef<ISaveDataDivisionExporter> outExporter,
            ISaveDataDivisionExporter.ExportContext exportContext)
        {
            using var exporterInterface = new SharedRef<FsSrv.Sf.ISaveDataDivisionExporter>();

            Result res = _baseInterface.Get.OpenSaveDataExporterByContext(ref exporterInterface.Ref(),
                InBuffer.FromStruct(in exportContext));

            _fsClient.Impl.LogResultErrorMessage(res);
            if (res.IsFailure()) return res.Miss();

            outExporter.Reset(new SaveDataExporterVersion2(_fsClient, ref exporterInterface.Ref()));
            return Result.Success;
        }

        public Result OpenSaveDataFullImporter(ref UniqueRef<ISaveDataDivisionImporter> outImporter,
            in InitialDataVersion2 initialData, in UserId userId, SaveDataSpaceId spaceId)
        {
            using var importerInterface = new SharedRef<FsSrv.Sf.ISaveDataDivisionImporter>();

            Result res = _baseInterface.Get.OpenSaveDataImporterDeprecated(ref importerInterface.Ref(),
                InBuffer.FromStruct(in initialData), in userId, spaceId);

            _fsClient.Impl.LogResultErrorMessage(res);
            if (res.IsFailure()) return res.Miss();

            outImporter.Reset(new SaveDataImporterVersion2(_fsClient, ref importerInterface.Ref()));
            return Result.Success;
        }

        public Result OpenSaveDataDiffImporter(ref UniqueRef<ISaveDataDivisionImporter> outImporter,
            in InitialDataVersion2 initialData, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            using var importerInterface = new SharedRef<FsSrv.Sf.ISaveDataDivisionImporter>();

            Result res = _baseInterface.Get.OpenSaveDataImporterForDiffImport(ref importerInterface.Ref(),
                InBuffer.FromStruct(in initialData), spaceId, saveDataId);

            _fsClient.Impl.LogResultErrorMessage(res);
            if (res.IsFailure()) return res.Miss();

            outImporter.Reset(new SaveDataImporterVersion2(_fsClient, ref importerInterface.Ref()));
            return Result.Success;
        }

        public Result OpenSaveDataDuplicateDiffImporter(ref UniqueRef<ISaveDataDivisionImporter> outImporter,
            in InitialDataVersion2 initialData, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            using var importerInterface = new SharedRef<FsSrv.Sf.ISaveDataDivisionImporter>();

            Result res = _baseInterface.Get.OpenSaveDataImporterForDuplicateDiffImport(ref importerInterface.Ref(),
                InBuffer.FromStruct(in initialData), spaceId, saveDataId);

            _fsClient.Impl.LogResultErrorMessage(res);
            if (res.IsFailure()) return res.Miss();

            outImporter.Reset(new SaveDataImporterVersion2(_fsClient, ref importerInterface.Ref()));
            return Result.Success;
        }

        public Result OpenSaveDataImporterImpl(ref UniqueRef<ISaveDataDivisionImporter> outImporter,
            in InitialDataVersion2 initialData, in UserId userId, SaveDataSpaceId spaceId, bool useSwap)
        {
            using var importerInterface = new SharedRef<FsSrv.Sf.ISaveDataDivisionImporter>();

            Result res = _baseInterface.Get.OpenSaveDataImporter(ref importerInterface.Ref(),
                InBuffer.FromStruct(in initialData), in userId, spaceId, useSwap);

            _fsClient.Impl.LogResultErrorMessage(res);
            if (res.IsFailure()) return res.Miss();

            outImporter.Reset(new SaveDataImporterVersion2(_fsClient, ref importerInterface.Ref()));
            return Result.Success;
        }

        public Result OpenSaveDataImporter(ref UniqueRef<ISaveDataDivisionImporter> outImporter,
            in InitialDataVersion2 initialData, SaveDataSpaceId spaceId, bool useSwap)
        {
            return OpenSaveDataImporterImpl(ref outImporter, in initialData, InvalidUserId, spaceId, useSwap);
        }

        public Result OpenSaveDataImporterByContext(ref UniqueRef<ISaveDataDivisionImporter> outImporter,
            in ISaveDataDivisionImporter.ImportContext importContext)
        {
            using var importerInterface = new SharedRef<FsSrv.Sf.ISaveDataDivisionImporter>();

            Result res = _baseInterface.Get.OpenSaveDataImporterByContext(ref importerInterface.Ref(),
                InBuffer.FromStruct(in importContext));

            _fsClient.Impl.LogResultErrorMessage(res);
            if (res.IsFailure()) return res.Miss();

            outImporter.Reset(new SaveDataImporterVersion2(_fsClient, ref importerInterface.Ref()));
            return Result.Success;
        }

        public static SaveDataTag MakeUserAccountSaveDataTag(Ncm.ApplicationId applicationId, in UserId userId)
        {
            Result res = SaveDataAttribute.Make(out SaveDataAttribute attribute, applicationId, SaveDataType.Account,
                userId, InvalidSystemSaveDataId, index: 0, SaveDataRank.Primary);
            Abort.DoAbortUnless(res.IsSuccess());

            return Unsafe.As<SaveDataAttribute, SaveDataTag>(ref attribute);
        }

        public static SaveDataTag MakeDeviceSaveDataTag(Ncm.ApplicationId applicationId)
        {
            Result res = SaveDataAttribute.Make(out SaveDataAttribute attribute, applicationId, SaveDataType.Device,
                InvalidUserId, InvalidSystemSaveDataId, index: 0, SaveDataRank.Primary);
            Abort.DoAbortUnless(res.IsSuccess());

            return Unsafe.As<SaveDataAttribute, SaveDataTag>(ref attribute);
        }

        public Result CancelSuspendingImport(in SaveDataTag tag)
        {
            ref readonly SaveDataAttribute attribute =
                ref Unsafe.As<SaveDataTag, SaveDataAttribute>(ref Unsafe.AsRef(in tag));

            Result res = _baseInterface.Get.CancelSuspendingImportByAttribute(in attribute);

            _fsClient.Impl.LogResultErrorMessage(res);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }

        public Result CancelSuspendingImport(Ncm.ApplicationId applicationId, in UserId userId)
        {
            Result res = _baseInterface.Get.CancelSuspendingImport(applicationId, in userId);

            _fsClient.Impl.LogResultErrorMessage(res);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }

        public Result SwapSecondary(in SaveDataTag tag, Optional<long> primaryCommitId)
        {
            long commitId = primaryCommitId.HasValue ? primaryCommitId.Value : 0;
            bool doSwap = primaryCommitId.HasValue;
            ref readonly SaveDataAttribute attribute =
                ref Unsafe.As<SaveDataTag, SaveDataAttribute>(ref Unsafe.AsRef(in tag));

            Result res = _baseInterface.Get.SwapSecondary(in attribute, doSwap, commitId);

            _fsClient.Impl.LogResultErrorMessage(res);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }
    }

    /// <summary>
    /// An adapter that automatically closes a given <see cref="FsSrv.Sf.ISaveDataTransferProhibiter"/>
    /// IPC service object when disposed.
    /// </summary>
    /// <remarks>Based on nnSdk 14.3.0</remarks>
    public class SaveDataTransferProhibiterForCloudBackUp : IDisposable
    {
        private SharedRef<ISaveDataTransferProhibiter> _prohibiter;

        public SaveDataTransferProhibiterForCloudBackUp(ref SharedRef<ISaveDataTransferProhibiter> prohibiter)
        {
            _prohibiter = SharedRef<ISaveDataTransferProhibiter>.CreateMove(ref prohibiter);
        }

        public void Dispose()
        {
            _prohibiter.Destroy();
        }
    }
}

namespace LibHac.Fs.Shim
{
    /// <summary>
    /// Contains functions used when doing save data cloud backup.
    /// </summary>
    /// <remarks>Based on nnSdk 14.3.0</remarks>
    public static class SaveDataTransferVersion2Shim
    {
        public static Result OpenSaveDataTransferProhibiterForCloudBackUp(this FileSystemClientImpl fs,
            ref UniqueRef<SaveDataTransferProhibiterForCloudBackUp> outProhibiter, Ncm.ApplicationId applicationId)
        {
            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.GetFileSystemProxyServiceObject();
            using var prohibiter = new SharedRef<ISaveDataTransferProhibiter>();

            // Todo: Uncomment once opening transfer prohibiters is implemented
            // Result res = fileSystemProxy.Get.OpenSaveDataTransferProhibiter(ref prohibiter.Ref(), applicationId);
            // if (res.IsFailure()) return res.Miss();

            outProhibiter.Reset(new SaveDataTransferProhibiterForCloudBackUp(ref prohibiter.Ref()));

            return Result.Success;
        }

        public static Result OpenSaveDataTransferProhibiterForCloudBackUp(this FileSystemClient fs,
            ref UniqueRef<SaveDataTransferProhibiterForCloudBackUp> outProhibiter, Ncm.ApplicationId applicationId)
        {
            Result res = fs.Impl.OpenSaveDataTransferProhibiterForCloudBackUp(ref outProhibiter, applicationId);
            fs.Impl.LogResultErrorMessage(res);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }

        public static Result OpenSaveDataTransferProhibiterForCloudBackUp(this FileSystemClient fs,
            Span<UniqueRef<SaveDataTransferProhibiterForCloudBackUp>> outProhibiters,
            ReadOnlySpan<Ncm.ApplicationId> applicationIds)
        {
            for (int i = 0; i < applicationIds.Length; i++)
            {
                Result res = fs.Impl.OpenSaveDataTransferProhibiterForCloudBackUp(ref outProhibiters[i],
                    applicationIds[i]);

                fs.Impl.LogResultErrorMessage(res);
                if (res.IsFailure()) return res.Miss();
            }

            return Result.Success;
        }

        public static Result GetCountOfApplicationAccessibleSaveDataOwnerId(this FileSystemClient fs, out int outCount,
            Ncm.ApplicationId applicationId, int programIndex)
        {
            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
            ulong tempAppId = 0;
            var programId = new Ncm.ProgramId(applicationId.Value + (uint)programIndex);

            Result res = fileSystemProxy.Get.ListAccessibleSaveDataOwnerId(out outCount,
                OutBuffer.FromStruct(ref tempAppId), programId, startIndex: 0, bufferIdCount: 0);

            fs.Impl.LogResultErrorMessage(res);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }

        public static Result GetOccupiedWorkSpaceSizeForCloudBackUp(this FileSystemClient fs, out long outSize)
        {
            UnsafeHelpers.SkipParamInit(out outSize);

            using var iterator = new UniqueRef<SaveDataIterator>();

            // We want to iterate every save with a Secondary rank
            Result res = SaveDataFilter.Make(out SaveDataFilter filter, programId: default, saveType: default,
                userId: default, saveDataId: default, index: default, SaveDataRank.Secondary);

            fs.Impl.LogResultErrorMessage(res);
            if (res.IsFailure()) return res.Miss();

            res = fs.Impl.OpenSaveDataIterator(ref iterator.Ref(), SaveDataSpaceId.User, in filter);
            fs.Impl.LogResultErrorMessage(res);
            if (res.IsFailure()) return res.Miss();

            long workSize = 0;

            while (true)
            {
                Unsafe.SkipInit(out SaveDataInfo info);

                res = fs.Impl.ReadSaveDataIteratorSaveDataInfo(out long count, SpanHelpers.AsSpan(ref info),
                    iterator.Get);

                fs.Impl.LogResultErrorMessage(res);
                if (res.IsFailure()) return res.Miss();

                // Break once we've iterated all saves
                if (count == 0)
                    break;

                if (info.Rank == SaveDataRank.Secondary)
                    workSize += info.Size;
            }

            outSize = workSize;
            return Result.Success;
        }
    }
}