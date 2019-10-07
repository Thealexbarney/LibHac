using System;
using LibHac.Fs;

namespace LibHac.FsService.Creators
{
    class EmulatedGameCardStorageCreator : IGameCardStorageCreator
    {
        private EmulatedGameCard GameCard { get; }

        public EmulatedGameCardStorageCreator(EmulatedGameCard gameCard)
        {
            GameCard = gameCard;
        }

        public Result CreateNormal(GameCardHandle handle, out IStorage storage)
        {
            storage = default;

            if (GameCard.IsGameCardHandleInvalid(handle))
            {
                return ResultFs.InvalidGameCardHandleOnOpenNormalPartition.Log();
            }

            var baseStorage = new ReadOnlyGameCardStorage(GameCard, handle);

            Result rc = GameCard.GetCardInfo(out GameCardInfo cardInfo, handle);
            if (rc.IsFailure()) return rc;

            storage = new SubStorage2(baseStorage, 0, cardInfo.SecureAreaOffset);
            return Result.Success;
        }

        public Result CreateSecure(GameCardHandle handle, out IStorage storage)
        {
            storage = default;

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

            var baseStorage = new ReadOnlyGameCardStorage(GameCard, handle, deviceId, imageHash);

            rc = GameCard.GetCardInfo(out GameCardInfo cardInfo, handle);
            if (rc.IsFailure()) return rc;

            storage = new SubStorage2(baseStorage, cardInfo.SecureAreaOffset, cardInfo.SecureAreaSize);
            return Result.Success;
        }

        public Result CreateWritable(GameCardHandle handle, out IStorage storage)
        {
            throw new NotImplementedException();
        }

        private class ReadOnlyGameCardStorage : StorageBase
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

            public ReadOnlyGameCardStorage(EmulatedGameCard gameCard, GameCardHandle handle, ReadOnlySpan<byte> deviceId, ReadOnlySpan<byte> imageHash)
            {
                GameCard = gameCard;
                Handle = handle;
                IsSecureMode = true;
                deviceId.CopyTo(DeviceId);
                imageHash.CopyTo(ImageHash);
            }

            public override Result Read(long offset, Span<byte> destination)
            {
                // In secure mode, if Handle is old and the card's device ID and
                // header hash are still the same, Handle is updated to the new handle

                return GameCard.Read(Handle, offset, destination);
            }

            public override Result Write(long offset, ReadOnlySpan<byte> source)
            {
                return ResultFs.UnsupportedOperationInRoGameCardStorageWrite.Log();
            }

            public override Result Flush()
            {
                return Result.Success;
            }

            public override Result SetSize(long size)
            {
                return ResultFs.UnsupportedOperationInRoGameCardStorageSetSize.Log();
            }

            public override Result GetSize(out long size)
            {
                size = 0;

                Result rc = GameCard.GetCardInfo(out GameCardInfo info, Handle);
                if (rc.IsFailure()) return rc;

                size = info.Size;
                return Result.Success;
            }
        }
    }
}
