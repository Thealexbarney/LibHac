using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Util;

namespace LibHac.FsSrv.Impl
{
    public static class AccessControlGlobalMethods
    {
        public static void SetDebugFlagEnabled(this FileSystemServer fsSrv, bool isEnabled)
        {
            fsSrv.Globals.AccessControl.DebugFlag = isEnabled;
        }
    }

    internal struct AccessControlGlobals
    {
        public bool DebugFlag;
    }

    /// <summary>
    /// Controls access to FS resources for a single process.
    /// </summary>
    /// <remarks>Each process has it's own FS permissions. Every time a process tries to access various FS resources
    /// or perform certain actions, this class determines if the process has the permissions to do so.
    /// <br/>Based on FS 10.0.0 (nnSdk 10.4.0)</remarks>
    public class AccessControl
    {
        private FileSystemServer FsServer { get; }
        private ref AccessControlGlobals Globals => ref FsServer.Globals.AccessControl;

        private Optional<AccessControlBits> AccessBits { get; }
        private LinkedList<ContentOwnerInfo> ContentOwners { get; } = new LinkedList<ContentOwnerInfo>();
        private LinkedList<SaveDataOwnerInfo> SaveDataOwners { get; } = new LinkedList<SaveDataOwnerInfo>();

        public AccessControl(FileSystemServer fsServer, ReadOnlySpan<byte> accessControlData,
            ReadOnlySpan<byte> accessControlDescriptor) : this(fsServer, accessControlData, accessControlDescriptor,
            GetAccessBitsMask(fsServer.Globals.AccessControl.DebugFlag))
        { }

        public AccessControl(FileSystemServer fsServer, ReadOnlySpan<byte> accessControlData,
            ReadOnlySpan<byte> accessControlDescriptor, ulong accessFlagMask)
        {
            FsServer = fsServer;

            // No permissions are given if any of the access control buffers are empty
            if (accessControlData.IsEmpty || accessControlDescriptor.IsEmpty)
            {
                AccessBits = new AccessControlBits(0);
                return;
            }

            // Verify the buffers are at least the minimum size
            Abort.DoAbortUnless(accessControlData.Length >= Unsafe.SizeOf<AccessControlDataHeader>());
            Abort.DoAbortUnless(accessControlDescriptor.Length >= Unsafe.SizeOf<AccessControlDescriptor>());

            // Cast the input buffers to their respective struct types
            ref readonly AccessControlDescriptor descriptor =
                ref SpanHelpers.AsReadOnlyStruct<AccessControlDescriptor>(accessControlDescriptor);

            ref readonly AccessControlDataHeader data =
                ref SpanHelpers.AsReadOnlyStruct<AccessControlDataHeader>(accessControlData);

            // Verify that the versions match and are valid
            if (data.Version == 0 || data.Version != descriptor.Version)
            {
                AccessBits = new AccessControlBits(0);
                return;
            }

            AccessBits = new AccessControlBits(descriptor.AccessFlags & accessFlagMask & data.AccessFlags);

            // Verify the buffers are long enough to hold the content owner info
            Abort.DoAbortUnless(accessControlData.Length >= data.ContentOwnerInfoOffset + data.ContentOwnerInfoSize);
            Abort.DoAbortUnless(accessControlDescriptor.Length >= Unsafe.SizeOf<AccessControlDescriptor>() +
                descriptor.ContentOwnerIdCount * sizeof(ulong));

            // Read and validate the content owner IDs in the access control data
            if (data.ContentOwnerInfoSize > 0)
            {
                int infoCount =
                    BinaryPrimitives.ReadInt32LittleEndian(accessControlData.Slice(data.ContentOwnerInfoOffset));

                // Get the list of content owner IDs in the descriptor, if any
                ReadOnlySpan<ulong> allowedIds = MemoryMarshal.Cast<byte, ulong>(
                    accessControlDescriptor.Slice(Unsafe.SizeOf<AccessControlDescriptor>(),
                        descriptor.ContentOwnerIdCount * sizeof(ulong)));

                // Get the list of content owner IDs
                ReadOnlySpan<ulong> ids = MemoryMarshal.Cast<byte, ulong>(
                    accessControlData.Slice(data.ContentOwnerInfoOffset + sizeof(int), infoCount * sizeof(ulong)));

                // Verify the size in the header matches the actual size of the info
                Abort.DoAbortUnless(data.ContentOwnerInfoSize == infoCount * sizeof(long));

                foreach (ulong id in ids)
                {
                    bool isIdAllowed;

                    if (allowedIds.Length > 0)
                    {
                        // The descriptor contains a list of allowed content owner IDs. Check if the ID is in that list
                        isIdAllowed = allowedIds.IndexOf(id) != -1;
                    }
                    else
                    {
                        // The descriptor contains a range of allowed content owner IDs. Check if the ID is in that range
                        isIdAllowed = descriptor.ContentOwnerIdMin == 0 && descriptor.ContentOwnerIdMax == 0 ||
                                      id >= descriptor.ContentOwnerIdMin && id <= descriptor.ContentOwnerIdMax;
                    }


                    if (isIdAllowed)
                    {
                        ContentOwners.AddFirst(new ContentOwnerInfo(id));
                    }
                }
            }

            // Verify the buffers are long enough to hold the save data owner info
            Abort.DoAbortUnless(accessControlData.Length >= data.SaveDataOwnerInfoOffset + data.SaveDataOwnerInfoSize);
            Abort.DoAbortUnless(accessControlDescriptor.Length >= Unsafe.SizeOf<AccessControlDescriptor>() +
                descriptor.ContentOwnerIdCount * sizeof(ulong) + descriptor.SaveDataOwnerIdCount * sizeof(ulong));

            if (data.SaveDataOwnerInfoSize > 0)
            {
                int infoCount =
                    BinaryPrimitives.ReadInt32LittleEndian(accessControlData.Slice(data.SaveDataOwnerInfoOffset));

                // Get the list of save data owner IDs in the descriptor, if any
                int allowedIdsOffset = Unsafe.SizeOf<AccessControlDescriptor>() +
                                       descriptor.ContentOwnerIdCount * sizeof(ulong);
                ReadOnlySpan<ulong> allowedIds = MemoryMarshal.Cast<byte, ulong>(
                    accessControlDescriptor.Slice(allowedIdsOffset, descriptor.SaveDataOwnerIdCount * sizeof(ulong)));

                // Get the lists of savedata owner accessibilities and IDs
                ReadOnlySpan<byte> accessibilities =
                    accessControlData.Slice(data.SaveDataOwnerInfoOffset + sizeof(int), infoCount);

                // The ID list must be 4-byte aligned
                int idsOffset = Alignment.AlignUp(data.SaveDataOwnerInfoOffset + sizeof(int) + infoCount, 4);
                ReadOnlySpan<ulong> ids = MemoryMarshal.Cast<byte, ulong>(
                    accessControlData.Slice(idsOffset, infoCount * sizeof(ulong)));

                // Verify the size in the header matches the actual size of the info
                Abort.DoAbortUnless(data.SaveDataOwnerInfoSize ==
                                    idsOffset - data.SaveDataOwnerInfoOffset + infoCount * sizeof(long));

                for (int i = 0; i < ids.Length; i++)
                {
                    var accessibility = new Accessibility(accessibilities[i]);
                    ulong id = ids[i];

                    bool isIdAllowed;

                    if (allowedIds.Length > 0)
                    {
                        // The descriptor contains a list of allowed save data owner IDs. Check if the ID is in that list
                        isIdAllowed = allowedIds.IndexOf(id) != -1;
                    }
                    else
                    {
                        // The descriptor contains a range of allowed save data owner IDs. Check if the ID is in that range
                        isIdAllowed = descriptor.SaveDataOwnerIdMin == 0 && descriptor.SaveDataOwnerIdMax == 0 ||
                                      id >= descriptor.SaveDataOwnerIdMin && id <= descriptor.SaveDataOwnerIdMax;
                    }

                    if (isIdAllowed)
                    {
                        SaveDataOwners.AddFirst(new SaveDataOwnerInfo(id, accessibility));
                    }
                }
            }
        }

        private static ulong GetAccessBitsMask(bool isDebugMode)
        {
            return isDebugMode ? 0xFFFFFFFFFFFFFFFF : 0x3FFFFFFFFFFFFFFF;
        }

        public bool HasContentOwnerId(ulong ownerId)
        {
            foreach (ContentOwnerInfo info in ContentOwners)
            {
                if (info.Id == ownerId)
                    return true;
            }

            return false;
        }

        public Accessibility GetAccessibilitySaveDataOwnedBy(ulong ownerId)
        {
            foreach (SaveDataOwnerInfo info in SaveDataOwners)
            {
                if (info.Id == ownerId)
                    return info.Accessibility;
            }

            return new Accessibility(false, false);
        }

        public void ListSaveDataOwnedId(out int outCount, Span<Ncm.ApplicationId> outIds, int startIndex)
        {
            // If there's no output buffer, return the number of owned IDs
            if (outIds.Length == 0)
            {
                outCount = SaveDataOwners.Count;
                return;
            }

            int preCount = 0;
            int outIndex = 0;

            foreach (SaveDataOwnerInfo info in SaveDataOwners)
            {
                // Stop reading if the buffer's full
                if (outIndex == outIds.Length)
                    break;

                // Skip IDs until we get to startIndex
                if (preCount < startIndex)
                {
                    preCount++;
                }
                else
                {
                    // Write the ID to the buffer
                    outIds[outIndex] = new Ncm.ApplicationId(info.Id);
                    outIndex++;
                }
            }

            outCount = outIndex;
        }

        public bool CanCall(OperationType operation)
        {
            // ReSharper disable once PossibleInvalidOperationException
            AccessControlBits accessBits = AccessBits.Value;

            switch (operation)
            {
                case OperationType.InvalidateBisCache:
                    return accessBits.CanInvalidateBisCache();
                case OperationType.EraseMmc:
                    return accessBits.CanEraseMmc();
                case OperationType.GetGameCardDeviceCertificate:
                    return accessBits.CanGetGameCardDeviceCertificate();
                case OperationType.GetGameCardIdSet:
                    return accessBits.CanGetGameCardIdSet();
                case OperationType.FinalizeGameCardDriver:
                    return accessBits.CanFinalizeGameCardDriver();
                case OperationType.GetGameCardAsicInfo:
                    return accessBits.CanGetGameCardAsicInfo();
                case OperationType.CreateSaveData:
                    return accessBits.CanCreateSaveData();
                case OperationType.DeleteSaveData:
                    return accessBits.CanDeleteSaveData();
                case OperationType.CreateSystemSaveData:
                    return accessBits.CanCreateSystemSaveData();
                case OperationType.CreateOthersSystemSaveData:
                    return accessBits.CanCreateOthersSystemSaveData();
                case OperationType.DeleteSystemSaveData:
                    return accessBits.CanDeleteSystemSaveData();
                case OperationType.OpenSaveDataInfoReader:
                    return accessBits.CanOpenSaveDataInfoReader();
                case OperationType.OpenSaveDataInfoReaderForSystem:
                    return accessBits.CanOpenSaveDataInfoReaderForSystem();
                case OperationType.OpenSaveDataInfoReaderForInternal:
                    return accessBits.CanOpenSaveDataInfoReaderForInternal();
                case OperationType.OpenSaveDataMetaFile:
                    return accessBits.CanOpenSaveDataMetaFile();
                case OperationType.SetCurrentPosixTime:
                    return accessBits.CanSetCurrentPosixTime();
                case OperationType.ReadSaveDataFileSystemExtraData:
                    return accessBits.CanReadSaveDataFileSystemExtraData();
                case OperationType.SetGlobalAccessLogMode:
                    return accessBits.CanSetGlobalAccessLogMode();
                case OperationType.SetSpeedEmulationMode:
                    return accessBits.CanSetSpeedEmulationMode();
                case OperationType.FillBis:
                    return accessBits.CanFillBis();
                case OperationType.CorruptSaveData:
                    return accessBits.CanCorruptSaveData();
                case OperationType.CorruptSystemSaveData:
                    return accessBits.CanCorruptSystemSaveData();
                case OperationType.VerifySaveData:
                    return accessBits.CanVerifySaveData();
                case OperationType.DebugSaveData:
                    return accessBits.CanDebugSaveData();
                case OperationType.FormatSdCard:
                    return accessBits.CanFormatSdCard();
                case OperationType.GetRightsId:
                    return accessBits.CanGetRightsId();
                case OperationType.RegisterExternalKey:
                    return accessBits.CanRegisterExternalKey();
                case OperationType.SetEncryptionSeed:
                    return accessBits.CanSetEncryptionSeed();
                case OperationType.WriteSaveDataFileSystemExtraDataTimeStamp:
                    return accessBits.CanWriteSaveDataFileSystemExtraDataTimeStamp();
                case OperationType.WriteSaveDataFileSystemExtraDataFlags:
                    return accessBits.CanWriteSaveDataFileSystemExtraDataFlags();
                case OperationType.WriteSaveDataFileSystemExtraDataCommitId:
                    return accessBits.CanWriteSaveDataFileSystemExtraDataCommitId();
                case OperationType.WriteSaveDataFileSystemExtraDataAll:
                    return accessBits.CanWriteSaveDataFileSystemExtraDataAll();
                case OperationType.ExtendSaveData:
                    return accessBits.CanExtendSaveData();
                case OperationType.ExtendSystemSaveData:
                    return accessBits.CanExtendSystemSaveData();
                case OperationType.ExtendOthersSystemSaveData:
                    return accessBits.CanExtendOthersSystemSaveData();
                case OperationType.RegisterUpdatePartition:
                    return accessBits.CanRegisterUpdatePartition() && Globals.DebugFlag;
                case OperationType.OpenSaveDataTransferManager:
                    return accessBits.CanOpenSaveDataTransferManager();
                case OperationType.OpenSaveDataTransferManagerVersion2:
                    return accessBits.CanOpenSaveDataTransferManagerVersion2();
                case OperationType.OpenSaveDataTransferManagerForSaveDataRepair:
                    return accessBits.CanOpenSaveDataTransferManagerForSaveDataRepair();
                case OperationType.OpenSaveDataTransferManagerForSaveDataRepairTool:
                    return accessBits.CanOpenSaveDataTransferManagerForSaveDataRepairTool();
                case OperationType.OpenSaveDataTransferProhibiter:
                    return accessBits.CanOpenSaveDataTransferProhibiter();
                case OperationType.OpenSaveDataMover:
                    return accessBits.CanOpenSaveDataMover();
                case OperationType.OpenBisWiper:
                    return accessBits.CanOpenBisWiper();
                case OperationType.ListAccessibleSaveDataOwnerId:
                    return accessBits.CanListAccessibleSaveDataOwnerId();
                case OperationType.ControlMmcPatrol:
                    return accessBits.CanControlMmcPatrol();
                case OperationType.OverrideSaveDataTransferTokenSignVerificationKey:
                    return accessBits.CanOverrideSaveDataTransferTokenSignVerificationKey();
                case OperationType.OpenSdCardDetectionEventNotifier:
                    return accessBits.CanOpenSdCardDetectionEventNotifier();
                case OperationType.OpenGameCardDetectionEventNotifier:
                    return accessBits.CanOpenGameCardDetectionEventNotifier();
                case OperationType.OpenSystemDataUpdateEventNotifier:
                    return accessBits.CanOpenSystemDataUpdateEventNotifier();
                case OperationType.NotifySystemDataUpdateEvent:
                    return accessBits.CanNotifySystemDataUpdateEvent();
                case OperationType.OpenAccessFailureDetectionEventNotifier:
                    return accessBits.CanOpenAccessFailureDetectionEventNotifier();
                case OperationType.GetAccessFailureDetectionEvent:
                    return accessBits.CanGetAccessFailureDetectionEvent();
                case OperationType.IsAccessFailureDetected:
                    return accessBits.CanIsAccessFailureDetected();
                case OperationType.ResolveAccessFailure:
                    return accessBits.CanResolveAccessFailure();
                case OperationType.AbandonAccessFailure:
                    return accessBits.CanAbandonAccessFailure();
                case OperationType.QuerySaveDataInternalStorageTotalSize:
                    return accessBits.CanQuerySaveDataInternalStorageTotalSize();
                case OperationType.GetSaveDataCommitId:
                    return accessBits.CanGetSaveDataCommitId();
                case OperationType.SetSdCardAccessibility:
                    return accessBits.CanSetSdCardAccessibility();
                case OperationType.SimulateDevice:
                    return accessBits.CanSimulateDevice();
                case OperationType.CreateSaveDataWithHashSalt:
                    return accessBits.CanCreateSaveDataWithHashSalt();
                case OperationType.RegisterProgramIndexMapInfo:
                    return accessBits.CanRegisterProgramIndexMapInfo();
                case OperationType.ChallengeCardExistence:
                    return accessBits.CanChallengeCardExistence();
                case OperationType.CreateOwnSaveData:
                    return accessBits.CanCreateOwnSaveData();
                case OperationType.ReadOwnSaveDataFileSystemExtraData:
                    return accessBits.CanReadOwnSaveDataFileSystemExtraData();
                case OperationType.ExtendOwnSaveData:
                    return accessBits.CanExtendOwnSaveData();
                case OperationType.OpenOwnSaveDataTransferProhibiter:
                    return accessBits.CanOpenOwnSaveDataTransferProhibiter();
                case OperationType.FindOwnSaveDataWithFilter:
                    return accessBits.CanFindOwnSaveDataWithFilter();
                default:
                    Abort.UnexpectedDefault();
                    return default;
            }
        }

        public Accessibility GetAccessibilityFor(AccessibilityType type)
        {
            // ReSharper disable once PossibleInvalidOperationException
            AccessControlBits accessBits = AccessBits.Value;

            switch (type)
            {
                case AccessibilityType.MountLogo:
                    return new Accessibility(accessBits.CanMountLogoRead(), false);
                case AccessibilityType.MountContentMeta:
                    return new Accessibility(accessBits.CanMountContentMetaRead(), false);
                case AccessibilityType.MountContentControl:
                    return new Accessibility(accessBits.CanMountContentControlRead(), false);
                case AccessibilityType.MountContentManual:
                    return new Accessibility(accessBits.CanMountContentManualRead(), false);
                case AccessibilityType.MountContentData:
                    return new Accessibility(accessBits.CanMountContentDataRead(), false);
                case AccessibilityType.MountApplicationPackage:
                    return new Accessibility(accessBits.CanMountApplicationPackageRead(), false);
                case AccessibilityType.MountSaveDataStorage:
                    return new Accessibility(accessBits.CanMountSaveDataStorageRead(), accessBits.CanMountSaveDataStorageWrite());
                case AccessibilityType.MountContentStorage:
                    return new Accessibility(accessBits.CanMountContentStorageRead(), accessBits.CanMountContentStorageWrite());
                case AccessibilityType.MountImageAndVideoStorage:
                    return new Accessibility(accessBits.CanMountImageAndVideoStorageRead(), accessBits.CanMountImageAndVideoStorageWrite());
                case AccessibilityType.MountCloudBackupWorkStorage:
                    return new Accessibility(accessBits.CanMountCloudBackupWorkStorageRead(), accessBits.CanMountCloudBackupWorkStorageWrite());
                case AccessibilityType.MountCustomStorage:
                    return new Accessibility(accessBits.CanMountCustomStorage0Read(), accessBits.CanMountCustomStorage0Write());
                case AccessibilityType.MountBisCalibrationFile:
                    return new Accessibility(accessBits.CanMountBisCalibrationFileRead(), accessBits.CanMountBisCalibrationFileWrite());
                case AccessibilityType.MountBisSafeMode:
                    return new Accessibility(accessBits.CanMountBisSafeModeRead(), accessBits.CanMountBisSafeModeWrite());
                case AccessibilityType.MountBisUser:
                    return new Accessibility(accessBits.CanMountBisUserRead(), accessBits.CanMountBisUserWrite());
                case AccessibilityType.MountBisSystem:
                    return new Accessibility(accessBits.CanMountBisSystemRead(), accessBits.CanMountBisSystemWrite());
                case AccessibilityType.MountBisSystemProperEncryption:
                    return new Accessibility(accessBits.CanMountBisSystemProperEncryptionRead(), accessBits.CanMountBisSystemProperEncryptionWrite());
                case AccessibilityType.MountBisSystemProperPartition:
                    return new Accessibility(accessBits.CanMountBisSystemProperPartitionRead(), accessBits.CanMountBisSystemProperPartitionWrite());
                case AccessibilityType.MountSdCard:
                    return new Accessibility(accessBits.CanMountSdCardRead(), accessBits.CanMountSdCardWrite());
                case AccessibilityType.MountGameCard:
                    return new Accessibility(accessBits.CanMountGameCardRead(), false);
                case AccessibilityType.MountDeviceSaveData:
                    return new Accessibility(accessBits.CanMountDeviceSaveDataRead(), accessBits.CanMountDeviceSaveDataWrite());
                case AccessibilityType.MountSystemSaveData:
                    return new Accessibility(accessBits.CanMountSystemSaveDataRead(), accessBits.CanMountSystemSaveDataWrite());
                case AccessibilityType.MountOthersSaveData:
                    return new Accessibility(accessBits.CanMountOthersSaveDataRead(), accessBits.CanMountOthersSaveDataWrite());
                case AccessibilityType.MountOthersSystemSaveData:
                    return new Accessibility(accessBits.CanMountOthersSystemSaveDataRead(), accessBits.CanMountOthersSystemSaveDataWrite());
                case AccessibilityType.OpenBisPartitionBootPartition1Root:
                    return new Accessibility(accessBits.CanOpenBisPartitionBootPartition1RootRead(), accessBits.CanOpenBisPartitionBootPartition1RootWrite());
                case AccessibilityType.OpenBisPartitionBootPartition2Root:
                    return new Accessibility(accessBits.CanOpenBisPartitionBootPartition2RootRead(), accessBits.CanOpenBisPartitionBootPartition2RootWrite());
                case AccessibilityType.OpenBisPartitionUserDataRoot:
                    return new Accessibility(accessBits.CanOpenBisPartitionUserDataRootRead(), accessBits.CanOpenBisPartitionUserDataRootWrite());
                case AccessibilityType.OpenBisPartitionBootConfigAndPackage2Part1:
                    return new Accessibility(accessBits.CanOpenBisPartitionBootConfigAndPackage2Part1Read(), accessBits.CanOpenBisPartitionBootConfigAndPackage2Part1Write());
                case AccessibilityType.OpenBisPartitionBootConfigAndPackage2Part2:
                    return new Accessibility(accessBits.CanOpenBisPartitionBootConfigAndPackage2Part2Read(), accessBits.CanOpenBisPartitionBootConfigAndPackage2Part2Write());
                case AccessibilityType.OpenBisPartitionBootConfigAndPackage2Part3:
                    return new Accessibility(accessBits.CanOpenBisPartitionBootConfigAndPackage2Part3Read(), accessBits.CanOpenBisPartitionBootConfigAndPackage2Part3Write());
                case AccessibilityType.OpenBisPartitionBootConfigAndPackage2Part4:
                    return new Accessibility(accessBits.CanOpenBisPartitionBootConfigAndPackage2Part4Read(), accessBits.CanOpenBisPartitionBootConfigAndPackage2Part4Write());
                case AccessibilityType.OpenBisPartitionBootConfigAndPackage2Part5:
                    return new Accessibility(accessBits.CanOpenBisPartitionBootConfigAndPackage2Part5Read(), accessBits.CanOpenBisPartitionBootConfigAndPackage2Part5Write());
                case AccessibilityType.OpenBisPartitionBootConfigAndPackage2Part6:
                    return new Accessibility(accessBits.CanOpenBisPartitionBootConfigAndPackage2Part6Read(), accessBits.CanOpenBisPartitionBootConfigAndPackage2Part6Write());
                case AccessibilityType.OpenBisPartitionCalibrationBinary:
                    return new Accessibility(accessBits.CanOpenBisPartitionCalibrationBinaryRead(), accessBits.CanOpenBisPartitionCalibrationFileWrite());
                case AccessibilityType.OpenBisPartitionCalibrationFile:
                    return new Accessibility(accessBits.CanOpenBisPartitionCalibrationFileRead(), accessBits.CanOpenBisPartitionCalibrationBinaryWrite());
                case AccessibilityType.OpenBisPartitionSafeMode:
                    return new Accessibility(accessBits.CanOpenBisPartitionSafeModeRead(), accessBits.CanOpenBisPartitionSafeModeWrite());
                case AccessibilityType.OpenBisPartitionUser:
                    return new Accessibility(accessBits.CanOpenBisPartitionUserRead(), accessBits.CanOpenBisPartitionUserWrite());
                case AccessibilityType.OpenBisPartitionSystem:
                    return new Accessibility(accessBits.CanOpenBisPartitionSystemRead(), accessBits.CanOpenBisPartitionSystemWrite());
                case AccessibilityType.OpenBisPartitionSystemProperEncryption:
                    return new Accessibility(accessBits.CanOpenBisPartitionSystemProperEncryptionRead(), accessBits.CanOpenBisPartitionSystemProperEncryptionWrite());
                case AccessibilityType.OpenBisPartitionSystemProperPartition:
                    return new Accessibility(accessBits.CanOpenBisPartitionSystemProperPartitionRead(), accessBits.CanOpenBisPartitionSystemProperPartitionWrite());
                case AccessibilityType.OpenSdCardStorage:
                    return new Accessibility(accessBits.CanOpenSdCardStorageRead(), accessBits.CanOpenSdCardStorageWrite());
                case AccessibilityType.OpenGameCardStorage:
                    return new Accessibility(accessBits.CanOpenGameCardStorageRead(), accessBits.CanOpenGameCardStorageWrite());
                case AccessibilityType.MountSystemDataPrivate:
                    return new Accessibility(accessBits.CanMountSystemDataPrivateRead(), false);
                case AccessibilityType.MountHost:
                    return new Accessibility(accessBits.CanMountHostRead(), accessBits.CanMountHostWrite());
                case AccessibilityType.MountRegisteredUpdatePartition:
                    return new Accessibility(accessBits.CanMountRegisteredUpdatePartitionRead() && Globals.DebugFlag, false);
                case AccessibilityType.MountSaveDataInternalStorage:
                    return new Accessibility(accessBits.CanOpenSaveDataInternalStorageRead(), accessBits.CanOpenSaveDataInternalStorageWrite());
                case AccessibilityType.MountTemporaryDirectory:
                    return new Accessibility(accessBits.CanMountTemporaryDirectoryRead(), accessBits.CanMountTemporaryDirectoryWrite());
                case AccessibilityType.MountAllBaseFileSystem:
                    return new Accessibility(accessBits.CanMountAllBaseFileSystemRead(), accessBits.CanMountAllBaseFileSystemWrite());
                case AccessibilityType.NotMount:
                    return new Accessibility(false, false);
                default:
                    Abort.UnexpectedDefault();
                    return default;
            }
        }
    }

    internal readonly struct ContentOwnerInfo
    {
        public readonly ulong Id;

        public ContentOwnerInfo(ulong id)
        {
            Id = id;
        }
    }

    internal readonly struct SaveDataOwnerInfo
    {
        public readonly ulong Id;
        public readonly Accessibility Accessibility;

        public SaveDataOwnerInfo(ulong id, Accessibility accessibility)
        {
            Id = id;
            Accessibility = accessibility;
        }
    }

    public readonly struct Accessibility
    {
        private readonly byte _value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Accessibility(bool canRead, bool canWrite)
        {
            int readValue = canRead ? 1 : 0;
            int writeValue = canWrite ? 1 : 0;
            _value = (byte)(writeValue << 1 | readValue);
        }

        public Accessibility(byte value)
        {
            _value = value;
        }

        public bool CanRead => (_value & 1) == 1;
        public bool CanWrite => ((_value >> 1) & 1) == 1;
    }

    public readonly struct AccessControlBits
    {
        public readonly ulong Value;

        public AccessControlBits(ulong value)
        {
            Value = value;
        }

        [Flags]
        public enum Bits : ulong
        {
            None = 0,
            ApplicationInfo = 1UL << 0,
            BootModeControl = 1UL << 1,
            Calibration = 1UL << 2,
            SystemSaveData = 1UL << 3,
            GameCard = 1UL << 4,
            SaveDataBackUp = 1UL << 5,
            SaveDataManagement = 1UL << 6,
            BisAllRaw = 1UL << 7,
            GameCardRaw = 1UL << 8,
            GameCardPrivate = 1UL << 9,
            SetTime = 1UL << 10,
            ContentManager = 1UL << 11,
            ImageManager = 1UL << 12,
            CreateSaveData = 1UL << 13,
            SystemSaveDataManagement = 1UL << 14,
            BisFileSystem = 1UL << 15,
            SystemUpdate = 1UL << 16,
            SaveDataMeta = 1UL << 17,
            DeviceSaveData = 1UL << 18,
            SettingsControl = 1UL << 19,
            SystemData = 1UL << 20,
            SdCard = 1UL << 21,
            Host = 1UL << 22,
            FillBis = 1UL << 23,
            CorruptSaveData = 1UL << 24,
            SaveDataForDebug = 1UL << 25,
            FormatSdCard = 1UL << 26,
            GetRightsId = 1UL << 27,
            RegisterExternalKey = 1UL << 28,
            RegisterUpdatePartition = 1UL << 29,
            SaveDataTransfer = 1UL << 30,
            DeviceDetection = 1UL << 31,
            AccessFailureResolution = 1UL << 32,
            SaveDataTransferVersion2 = 1UL << 33,
            RegisterProgramIndexMapInfo = 1UL << 34,
            CreateOwnSaveData = 1UL << 35,
            MoveCacheStorage = 1UL << 36,
            Debug = 1UL << 62,
            FullPermission = 1UL << 63
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool Has(Bits bits)
        {
            return ((Bits)Value & (Bits.FullPermission | bits)) != 0;
        }

        public bool CanAbandonAccessFailure() => Has(Bits.AccessFailureResolution);
        public bool CanChallengeCardExistence() => Has(Bits.GameCard);
        public bool CanControlMmcPatrol() => Has(Bits.None);
        public bool CanCorruptSaveData() => Has(Bits.Debug | Bits.CorruptSaveData);
        public bool CanCorruptSystemSaveData() => Has(Bits.CorruptSaveData | Bits.SaveDataManagement | Bits.SaveDataBackUp);
        public bool CanCreateOthersSystemSaveData() => Has(Bits.SaveDataBackUp);
        public bool CanCreateOwnSaveData() => Has(Bits.CreateOwnSaveData);
        public bool CanCreateSaveData() => Has(Bits.CreateSaveData | Bits.SaveDataBackUp);
        public bool CanCreateSaveDataWithHashSalt() => Has(Bits.None);
        public bool CanCreateSystemSaveData() => Has(Bits.SaveDataBackUp | Bits.SystemSaveData);
        public bool CanDebugSaveData() => Has(Bits.Debug | Bits.SaveDataForDebug);
        public bool CanDeleteSaveData() => Has(Bits.SaveDataManagement | Bits.SaveDataBackUp);
        public bool CanDeleteSystemSaveData() => Has(Bits.SystemSaveDataManagement | Bits.SaveDataBackUp | Bits.SystemSaveData);
        public bool CanEraseMmc() => Has(Bits.BisAllRaw);
        public bool CanExtendOthersSystemSaveData() => Has(Bits.SaveDataBackUp);
        public bool CanExtendOwnSaveData() => Has(Bits.CreateOwnSaveData);
        public bool CanExtendSaveData() => Has(Bits.CreateSaveData | Bits.SaveDataBackUp);
        public bool CanExtendSystemSaveData() => Has(Bits.SaveDataBackUp | Bits.SystemSaveData);
        public bool CanFillBis() => Has(Bits.Debug | Bits.FillBis);
        public bool CanFinalizeGameCardDriver() => Has(Bits.GameCardPrivate);
        public bool CanFindOwnSaveDataWithFilter() => Has(Bits.CreateOwnSaveData);
        public bool CanFormatSdCard() => Has(Bits.FormatSdCard);
        public bool CanGetAccessFailureDetectionEvent() => Has(Bits.AccessFailureResolution);
        public bool CanGetGameCardAsicInfo() => Has(Bits.GameCardPrivate);
        public bool CanGetGameCardDeviceCertificate() => Has(Bits.GameCard);
        public bool CanGetGameCardIdSet() => Has(Bits.GameCard);
        public bool CanGetRightsId() => Has(Bits.GetRightsId);
        public bool CanGetSaveDataCommitId() => Has(Bits.SaveDataTransferVersion2 | Bits.SaveDataBackUp);
        public bool CanInvalidateBisCache() => Has(Bits.BisAllRaw);
        public bool CanIsAccessFailureDetected() => Has(Bits.AccessFailureResolution);
        public bool CanListAccessibleSaveDataOwnerId() => Has(Bits.SaveDataTransferVersion2 | Bits.SaveDataTransfer | Bits.CreateSaveData);
        public bool CanMountAllBaseFileSystemRead() => Has(Bits.None);
        public bool CanMountAllBaseFileSystemWrite() => Has(Bits.None);
        public bool CanMountApplicationPackageRead() => Has(Bits.ContentManager | Bits.ApplicationInfo);
        public bool CanMountBisCalibrationFileRead() => Has(Bits.BisAllRaw | Bits.Calibration);
        public bool CanMountBisCalibrationFileWrite() => Has(Bits.BisAllRaw | Bits.Calibration);
        public bool CanMountBisSafeModeRead() => Has(Bits.BisAllRaw);
        public bool CanMountBisSafeModeWrite() => Has(Bits.BisAllRaw);
        public bool CanMountBisSystemProperEncryptionRead() => Has(Bits.BisAllRaw);
        public bool CanMountBisSystemProperEncryptionWrite() => Has(Bits.BisAllRaw);
        public bool CanMountBisSystemProperPartitionRead() => Has(Bits.BisFileSystem | Bits.BisAllRaw);
        public bool CanMountBisSystemProperPartitionWrite() => Has(Bits.BisFileSystem | Bits.BisAllRaw);
        public bool CanMountBisSystemRead() => Has(Bits.BisFileSystem | Bits.BisAllRaw);
        public bool CanMountBisSystemWrite() => Has(Bits.BisFileSystem | Bits.BisAllRaw);
        public bool CanMountBisUserRead() => Has(Bits.BisFileSystem | Bits.BisAllRaw);
        public bool CanMountBisUserWrite() => Has(Bits.BisFileSystem | Bits.BisAllRaw);
        public bool CanMountCloudBackupWorkStorageRead() => Has(Bits.SaveDataTransferVersion2);
        public bool CanMountCloudBackupWorkStorageWrite() => Has(Bits.SaveDataTransferVersion2);
        public bool CanMountContentControlRead() => Has(Bits.ContentManager | Bits.ApplicationInfo);
        public bool CanMountContentDataRead() => Has(Bits.ContentManager | Bits.ApplicationInfo);
        public bool CanMountContentManualRead() => Has(Bits.ContentManager | Bits.ApplicationInfo);
        public bool CanMountContentMetaRead() => Has(Bits.ContentManager | Bits.ApplicationInfo);
        public bool CanMountContentStorageRead() => Has(Bits.ContentManager);
        public bool CanMountContentStorageWrite() => Has(Bits.ContentManager);
        public bool CanMountCustomStorage0Read() => Has(Bits.None);
        public bool CanMountCustomStorage0Write() => Has(Bits.None);
        public bool CanMountDeviceSaveDataRead() => Has(Bits.DeviceSaveData | Bits.SaveDataBackUp);
        public bool CanMountDeviceSaveDataWrite() => Has(Bits.DeviceSaveData | Bits.SaveDataBackUp);
        public bool CanMountGameCardRead() => Has(Bits.GameCard);
        public bool CanMountHostRead() => Has(Bits.Debug | Bits.Host);
        public bool CanMountHostWrite() => Has(Bits.Debug | Bits.Host);
        public bool CanMountImageAndVideoStorageRead() => Has(Bits.ImageManager);
        public bool CanMountImageAndVideoStorageWrite() => Has(Bits.ImageManager);
        public bool CanMountLogoRead() => Has(Bits.ContentManager | Bits.ApplicationInfo);
        public bool CanMountOthersSaveDataRead() => Has(Bits.SaveDataBackUp);
        public bool CanMountOthersSaveDataWrite() => Has(Bits.SaveDataBackUp);
        public bool CanMountOthersSystemSaveDataRead() => Has(Bits.SaveDataBackUp);
        public bool CanMountOthersSystemSaveDataWrite() => Has(Bits.SaveDataBackUp);
        public bool CanMountRegisteredUpdatePartitionRead() => Has(Bits.SystemUpdate);
        public bool CanMountSaveDataStorageRead() => Has(Bits.None);
        public bool CanMountSaveDataStorageWrite() => Has(Bits.None);
        public bool CanMountSdCardRead() => Has(Bits.Debug | Bits.SdCard);
        public bool CanMountSdCardWrite() => Has(Bits.Debug | Bits.SdCard);
        public bool CanMountSystemDataPrivateRead() => Has(Bits.SystemData | Bits.SystemSaveData);
        public bool CanMountSystemSaveDataRead() => Has(Bits.SaveDataBackUp | Bits.SystemSaveData);
        public bool CanMountSystemSaveDataWrite() => Has(Bits.SaveDataBackUp | Bits.SystemSaveData);
        public bool CanMountTemporaryDirectoryRead() => Has(Bits.Debug);
        public bool CanMountTemporaryDirectoryWrite() => Has(Bits.Debug);
        public bool CanNotifySystemDataUpdateEvent() => Has(Bits.SystemUpdate);
        public bool CanOpenAccessFailureDetectionEventNotifier() => Has(Bits.AccessFailureResolution);
        public bool CanOpenBisPartitionBootConfigAndPackage2Part1Read() => Has(Bits.SystemUpdate | Bits.BisAllRaw);
        public bool CanOpenBisPartitionBootConfigAndPackage2Part1Write() => Has(Bits.SystemUpdate | Bits.BisAllRaw);
        public bool CanOpenBisPartitionBootConfigAndPackage2Part2Read() => Has(Bits.SystemUpdate | Bits.BisAllRaw);
        public bool CanOpenBisPartitionBootConfigAndPackage2Part2Write() => Has(Bits.SystemUpdate | Bits.BisAllRaw);
        public bool CanOpenBisPartitionBootConfigAndPackage2Part3Read() => Has(Bits.SystemUpdate | Bits.BisAllRaw);
        public bool CanOpenBisPartitionBootConfigAndPackage2Part3Write() => Has(Bits.SystemUpdate | Bits.BisAllRaw);
        public bool CanOpenBisPartitionBootConfigAndPackage2Part4Read() => Has(Bits.SystemUpdate | Bits.BisAllRaw);
        public bool CanOpenBisPartitionBootConfigAndPackage2Part4Write() => Has(Bits.SystemUpdate | Bits.BisAllRaw);
        public bool CanOpenBisPartitionBootConfigAndPackage2Part5Read() => Has(Bits.SystemUpdate | Bits.BisAllRaw);
        public bool CanOpenBisPartitionBootConfigAndPackage2Part5Write() => Has(Bits.SystemUpdate | Bits.BisAllRaw);
        public bool CanOpenBisPartitionBootConfigAndPackage2Part6Read() => Has(Bits.SystemUpdate | Bits.BisAllRaw);
        public bool CanOpenBisPartitionBootConfigAndPackage2Part6Write() => Has(Bits.SystemUpdate | Bits.BisAllRaw);
        public bool CanOpenBisPartitionBootPartition1RootRead() => Has(Bits.SystemUpdate | Bits.BisAllRaw | Bits.BootModeControl);
        public bool CanOpenBisPartitionBootPartition1RootWrite() => Has(Bits.SystemUpdate | Bits.BisAllRaw | Bits.BootModeControl);
        public bool CanOpenBisPartitionBootPartition2RootRead() => Has(Bits.SystemUpdate | Bits.BisAllRaw);
        public bool CanOpenBisPartitionBootPartition2RootWrite() => Has(Bits.SystemUpdate | Bits.BisAllRaw);
        public bool CanOpenBisPartitionCalibrationBinaryRead() => Has(Bits.BisAllRaw | Bits.Calibration);
        public bool CanOpenBisPartitionCalibrationBinaryWrite() => Has(Bits.BisAllRaw | Bits.Calibration);
        public bool CanOpenBisPartitionCalibrationFileRead() => Has(Bits.BisAllRaw | Bits.Calibration);
        public bool CanOpenBisPartitionCalibrationFileWrite() => Has(Bits.BisAllRaw | Bits.Calibration);
        public bool CanOpenBisPartitionSafeModeRead() => Has(Bits.BisAllRaw);
        public bool CanOpenBisPartitionSafeModeWrite() => Has(Bits.BisAllRaw);
        public bool CanOpenBisPartitionSystemProperEncryptionRead() => Has(Bits.BisAllRaw);
        public bool CanOpenBisPartitionSystemProperEncryptionWrite() => Has(Bits.BisAllRaw);
        public bool CanOpenBisPartitionSystemProperPartitionRead() => Has(Bits.BisAllRaw);
        public bool CanOpenBisPartitionSystemProperPartitionWrite() => Has(Bits.BisAllRaw);
        public bool CanOpenBisPartitionSystemRead() => Has(Bits.BisAllRaw);
        public bool CanOpenBisPartitionSystemWrite() => Has(Bits.BisAllRaw);
        public bool CanOpenBisPartitionUserDataRootRead() => Has(Bits.BisAllRaw);
        public bool CanOpenBisPartitionUserDataRootWrite() => Has(Bits.BisAllRaw);
        public bool CanOpenBisPartitionUserRead() => Has(Bits.BisAllRaw);
        public bool CanOpenBisPartitionUserWrite() => Has(Bits.BisAllRaw);
        public bool CanOpenBisWiper() => Has(Bits.ContentManager);
        public bool CanOpenGameCardDetectionEventNotifier() => Has(Bits.DeviceDetection | Bits.GameCardRaw | Bits.GameCard);
        public bool CanOpenGameCardStorageRead() => Has(Bits.GameCardRaw);
        public bool CanOpenGameCardStorageWrite() => Has(Bits.GameCardRaw);
        public bool CanOpenOwnSaveDataTransferProhibiter() => Has(Bits.CreateOwnSaveData);
        public bool CanOpenSaveDataInfoReader() => Has(Bits.SaveDataManagement | Bits.SaveDataBackUp);
        public bool CanOpenSaveDataInfoReaderForInternal() => Has(Bits.SaveDataManagement);
        public bool CanOpenSaveDataInfoReaderForSystem() => Has(Bits.SystemSaveDataManagement | Bits.SaveDataBackUp);
        public bool CanOpenSaveDataInternalStorageRead() => Has(Bits.None);
        public bool CanOpenSaveDataInternalStorageWrite() => Has(Bits.None);
        public bool CanOpenSaveDataMetaFile() => Has(Bits.SaveDataMeta);
        public bool CanOpenSaveDataMover() => Has(Bits.MoveCacheStorage);
        public bool CanOpenSaveDataTransferManager() => Has(Bits.SaveDataTransfer);
        public bool CanOpenSaveDataTransferManagerForSaveDataRepair() => Has(Bits.SaveDataTransferVersion2);
        public bool CanOpenSaveDataTransferManagerForSaveDataRepairTool() => Has(Bits.None);
        public bool CanOpenSaveDataTransferManagerVersion2() => Has(Bits.SaveDataTransferVersion2);
        public bool CanOpenSaveDataTransferProhibiter() => Has(Bits.SaveDataTransferVersion2 | Bits.CreateSaveData);
        public bool CanOpenSdCardDetectionEventNotifier() => Has(Bits.DeviceDetection | Bits.SdCard);
        public bool CanOpenSdCardStorageRead() => Has(Bits.Debug | Bits.SdCard);
        public bool CanOpenSdCardStorageWrite() => Has(Bits.Debug | Bits.SdCard);
        public bool CanOpenSystemDataUpdateEventNotifier() => Has(Bits.SystemData | Bits.SystemSaveData);
        public bool CanOverrideSaveDataTransferTokenSignVerificationKey() => Has(Bits.None);
        public bool CanQuerySaveDataInternalStorageTotalSize() => Has(Bits.SaveDataTransfer);
        public bool CanReadOwnSaveDataFileSystemExtraData() => Has(Bits.CreateOwnSaveData);
        public bool CanReadSaveDataFileSystemExtraData() => Has(Bits.SystemSaveDataManagement | Bits.SaveDataManagement | Bits.SaveDataBackUp);
        public bool CanRegisterExternalKey() => Has(Bits.RegisterExternalKey);
        public bool CanRegisterProgramIndexMapInfo() => Has(Bits.RegisterProgramIndexMapInfo);
        public bool CanRegisterUpdatePartition() => Has(Bits.RegisterUpdatePartition);
        public bool CanResolveAccessFailure() => Has(Bits.AccessFailureResolution);
        public bool CanSetCurrentPosixTime() => Has(Bits.SetTime);
        public bool CanSetEncryptionSeed() => Has(Bits.ContentManager);
        public bool CanSetGlobalAccessLogMode() => Has(Bits.SettingsControl);
        public bool CanSetSdCardAccessibility() => Has(Bits.SdCard);
        public bool CanSetSpeedEmulationMode() => Has(Bits.SettingsControl);
        public bool CanSimulateDevice() => Has(Bits.Debug);
        public bool CanVerifySaveData() => Has(Bits.SaveDataManagement | Bits.SaveDataBackUp);
        public bool CanWriteSaveDataFileSystemExtraDataAll() => Has(Bits.None);
        public bool CanWriteSaveDataFileSystemExtraDataCommitId() => Has(Bits.SaveDataBackUp);
        public bool CanWriteSaveDataFileSystemExtraDataFlags() => Has(Bits.SaveDataTransferVersion2 | Bits.SystemSaveDataManagement | Bits.SaveDataBackUp);
        public bool CanWriteSaveDataFileSystemExtraDataTimeStamp() => Has(Bits.SaveDataBackUp);
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x2C)]
    internal struct AccessControlDescriptor
    {
        [FieldOffset(0x00)] public byte Version;
        [FieldOffset(0x01)] public byte ContentOwnerIdCount;
        [FieldOffset(0x02)] public byte SaveDataOwnerIdCount;
        [FieldOffset(0x04)] public ulong AccessFlags;
        [FieldOffset(0x0C)] public ulong ContentOwnerIdMin;
        [FieldOffset(0x14)] public ulong ContentOwnerIdMax;
        [FieldOffset(0x1C)] public ulong SaveDataOwnerIdMin;
        [FieldOffset(0x24)] public ulong SaveDataOwnerIdMax;
        // public ulong ContentOwnerIds[ContentOwnerIdCount];
        // public ulong SaveDataOwnerIds[SaveDataOwnerIdCount];
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x1C)]
    internal struct AccessControlDataHeader
    {
        [FieldOffset(0x00)] public byte Version;
        [FieldOffset(0x04)] public ulong AccessFlags;
        [FieldOffset(0x0C)] public int ContentOwnerInfoOffset;
        [FieldOffset(0x10)] public int ContentOwnerInfoSize;
        [FieldOffset(0x14)] public int SaveDataOwnerInfoOffset;
        [FieldOffset(0x18)] public int SaveDataOwnerInfoSize;

        // [FieldOffset(ContentOwnerInfoOffset)]
        // public int ContentOwnerInfoCount;
        // public ulong ContentOwnerIds[ContentOwnerInfoCount];

        // [FieldOffset(SaveDataOwnerInfoOffset)]
        // public int SaveDataOwnerInfoCount;
        // public byte Accessibilities[SaveDataOwnerInfoCount];
        // Next field is 4-byte aligned
        // public byte SaveDataOwnerIds[SaveDataOwnerInfoCount];
    }

    public enum OperationType
    {
        InvalidateBisCache,
        EraseMmc,
        GetGameCardDeviceCertificate,
        GetGameCardIdSet,
        FinalizeGameCardDriver,
        GetGameCardAsicInfo,
        CreateSaveData,
        DeleteSaveData,
        CreateSystemSaveData,
        CreateOthersSystemSaveData,
        DeleteSystemSaveData,
        OpenSaveDataInfoReader,
        OpenSaveDataInfoReaderForSystem,
        OpenSaveDataInfoReaderForInternal,
        OpenSaveDataMetaFile,
        SetCurrentPosixTime,
        ReadSaveDataFileSystemExtraData,
        SetGlobalAccessLogMode,
        SetSpeedEmulationMode,
        Debug,
        FillBis,
        CorruptSaveData,
        CorruptSystemSaveData,
        VerifySaveData,
        DebugSaveData,
        FormatSdCard,
        GetRightsId,
        RegisterExternalKey,
        SetEncryptionSeed,
        WriteSaveDataFileSystemExtraDataTimeStamp,
        WriteSaveDataFileSystemExtraDataFlags,
        WriteSaveDataFileSystemExtraDataCommitId,
        WriteSaveDataFileSystemExtraDataAll,
        ExtendSaveData,
        ExtendSystemSaveData,
        ExtendOthersSystemSaveData,
        RegisterUpdatePartition,
        OpenSaveDataTransferManager,
        OpenSaveDataTransferManagerVersion2,
        OpenSaveDataTransferManagerForSaveDataRepair,
        OpenSaveDataTransferManagerForSaveDataRepairTool,
        OpenSaveDataTransferProhibiter,
        OpenSaveDataMover,
        OpenBisWiper,
        ListAccessibleSaveDataOwnerId,
        ControlMmcPatrol,
        OverrideSaveDataTransferTokenSignVerificationKey,
        OpenSdCardDetectionEventNotifier,
        OpenGameCardDetectionEventNotifier,
        OpenSystemDataUpdateEventNotifier,
        NotifySystemDataUpdateEvent,
        OpenAccessFailureDetectionEventNotifier,
        GetAccessFailureDetectionEvent,
        IsAccessFailureDetected,
        ResolveAccessFailure,
        AbandonAccessFailure,
        QuerySaveDataInternalStorageTotalSize,
        GetSaveDataCommitId,
        SetSdCardAccessibility,
        SimulateDevice,
        CreateSaveDataWithHashSalt,
        RegisterProgramIndexMapInfo,
        ChallengeCardExistence,
        CreateOwnSaveData,
        DeleteOwnSaveData,
        ReadOwnSaveDataFileSystemExtraData,
        ExtendOwnSaveData,
        OpenOwnSaveDataTransferProhibiter,
        FindOwnSaveDataWithFilter
    }

    public enum AccessibilityType
    {
        MountLogo,
        MountContentMeta,
        MountContentControl,
        MountContentManual,
        MountContentData,
        MountApplicationPackage,
        MountSaveDataStorage,
        MountContentStorage,
        MountImageAndVideoStorage,
        MountCloudBackupWorkStorage,
        MountCustomStorage,
        MountBisCalibrationFile,
        MountBisSafeMode,
        MountBisUser,
        MountBisSystem,
        MountBisSystemProperEncryption,
        MountBisSystemProperPartition,
        MountSdCard,
        MountGameCard,
        MountDeviceSaveData,
        MountSystemSaveData,
        MountOthersSaveData,
        MountOthersSystemSaveData,
        OpenBisPartitionBootPartition1Root,
        OpenBisPartitionBootPartition2Root,
        OpenBisPartitionUserDataRoot,
        OpenBisPartitionBootConfigAndPackage2Part1,
        OpenBisPartitionBootConfigAndPackage2Part2,
        OpenBisPartitionBootConfigAndPackage2Part3,
        OpenBisPartitionBootConfigAndPackage2Part4,
        OpenBisPartitionBootConfigAndPackage2Part5,
        OpenBisPartitionBootConfigAndPackage2Part6,
        OpenBisPartitionCalibrationBinary,
        OpenBisPartitionCalibrationFile,
        OpenBisPartitionSafeMode,
        OpenBisPartitionUser,
        OpenBisPartitionSystem,
        OpenBisPartitionSystemProperEncryption,
        OpenBisPartitionSystemProperPartition,
        OpenSdCardStorage,
        OpenGameCardStorage,
        MountSystemDataPrivate,
        MountHost,
        MountRegisteredUpdatePartition,
        MountSaveDataInternalStorage,
        MountTemporaryDirectory,
        MountAllBaseFileSystem,
        NotMount
    }
}
