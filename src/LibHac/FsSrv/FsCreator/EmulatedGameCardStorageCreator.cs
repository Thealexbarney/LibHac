using System;
using LibHac.Common;
using LibHac.Fs;

namespace LibHac.FsSrv.FsCreator
{
    public class EmulatedGameCardStorageCreator : IGameCardStorageCreator
    {
        private EmulatedGameCard GameCard { get; }

        public EmulatedGameCardStorageCreator(EmulatedGameCard gameCard)
        {
            GameCard = gameCard;
        }

        public Result CreateReadOnly(GameCardHandle handle, out ReferenceCountedDisposable<IStorage> storage)
        {
            UnsafeHelpers.SkipParamInit(out storage);

            if (GameCard.IsGameCardHandleInvalid(handle))
            {
                return ResultFs.InvalidGameCardHandleOnOpenNormalPartition.Log();
            }

            var baseStorage = new ReferenceCountedDisposable<IStorage>(new ReadOnlyGameCardStorage(GameCard, handle));

            Result rc = GameCard.GetCardInfo(out GameCardInfo cardInfo, handle);
            if (rc.IsFailure()) return rc;

            storage = new ReferenceCountedDisposable<IStorage>(
                new SubStorage(baseStorage, 0, cardInfo.SecureAreaOffset));
            return Result.Success;
        }

        public Result CreateSecureReadOnly(GameCardHandle handle, out ReferenceCountedDisposable<IStorage> storage)
        {
            UnsafeHelpers.SkipParamInit(out storage);

            if (GameCard.IsGameCardHandleInvalid(handle))
            {
                return ResultFs.InvalidGameCardHandleOnOpenSecurePartition.Log();
            }

            Span<byte> deviceId = stackalloc byte[0x10];
            Span<byte> imageHash = stackalloc byte[0x20];

            Result rc = GameCard.GetGameCardDeviceId(deviceId);
            if (rc.IsFailure()) return rc;

            rc = GameCard.GetGameCardImageHash(imageHash);
            if (rc.IsFailure()) return rc;

            var baseStorage =
                new ReferenceCountedDisposable<IStorage>(new ReadOnlyGameCardStorage(GameCard, handle, deviceId,
                    imageHash));

            rc = GameCard.GetCardInfo(out GameCardInfo cardInfo, handle);
            if (rc.IsFailure()) return rc;

            storage = new ReferenceCountedDisposable<IStorage>(new SubStorage(baseStorage, cardInfo.SecureAreaOffset,
                cardInfo.SecureAreaSize));
            return Result.Success;
        }

        public Result CreateWriteOnly(GameCardHandle handle, out ReferenceCountedDisposable<IStorage> storage)
        {
            throw new NotImplementedException();
        }

        private class ReadOnlyGameCardStorage : IStorage
        {
            private EmulatedGameCard GameCard { get; }
            private GameCardHandle Handle { get; set; }

            // ReSharper disable once UnusedAutoPropertyAccessor.Local
            private bool IsSecureMode { get; }
            private byte[] DeviceId { get; } = new byte[0x10];
            private byte[] ImageHash { get; } = new byte[0x20];

            public ReadOnlyGameCardStorage(EmulatedGameCard gameCard, GameCardHandle handle)
            {
                GameCard = gameCard;
                Handle = handle;
            }

            public ReadOnlyGameCardStorage(EmulatedGameCard gameCard, GameCardHandle handle,
                ReadOnlySpan<byte> deviceId, ReadOnlySpan<byte> imageHash)
            {
                GameCard = gameCard;
                Handle = handle;
                IsSecureMode = true;
                deviceId.CopyTo(DeviceId);
                imageHash.CopyTo(ImageHash);
            }

            protected override Result DoRead(long offset, Span<byte> destination)
            {
                // In secure mode, if Handle is old and the card's device ID and
                // header hash are still the same, Handle is updated to the new handle

                return GameCard.Read(Handle, offset, destination);
            }

            protected override Result DoWrite(long offset, ReadOnlySpan<byte> source)
            {
                return ResultFs.UnsupportedWriteForReadOnlyGameCardStorage.Log();
            }

            protected override Result DoFlush()
            {
                return Result.Success;
            }

            protected override Result DoSetSize(long size)
            {
                return ResultFs.UnsupportedSetSizeForReadOnlyGameCardStorage.Log();
            }

            protected override Result DoGetSize(out long size)
            {
                UnsafeHelpers.SkipParamInit(out size);

                Result rc = GameCard.GetCardInfo(out GameCardInfo info, Handle);
                if (rc.IsFailure()) return rc;

                size = info.Size;
                return Result.Success;
            }
        }
    }
}
