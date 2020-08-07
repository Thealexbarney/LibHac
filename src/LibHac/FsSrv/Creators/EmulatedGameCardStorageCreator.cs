﻿using System;
using LibHac.Fs;

namespace LibHac.FsSrv.Creators
{
    public class EmulatedGameCardStorageCreator : IGameCardStorageCreator
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

            storage = new SubStorage(baseStorage, 0, cardInfo.SecureAreaOffset);
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

            storage = new SubStorage(baseStorage, cardInfo.SecureAreaOffset, cardInfo.SecureAreaSize);
            return Result.Success;
        }

        public Result CreateWritable(GameCardHandle handle, out IStorage storage)
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

            public ReadOnlyGameCardStorage(EmulatedGameCard gameCard, GameCardHandle handle, ReadOnlySpan<byte> deviceId, ReadOnlySpan<byte> imageHash)
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
                return ResultFs.UnsupportedOperationInRoGameCardStorageWrite.Log();
            }

            protected override Result DoFlush()
            {
                return Result.Success;
            }

            protected override Result DoSetSize(long size)
            {
                return ResultFs.UnsupportedOperationInRoGameCardStorageSetSize.Log();
            }

            protected override Result DoGetSize(out long size)
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
