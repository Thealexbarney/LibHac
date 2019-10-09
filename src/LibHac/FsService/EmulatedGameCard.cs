using System;
using LibHac.Fs;

namespace LibHac.FsService
{
    public class EmulatedGameCard
    {
        private IStorage CardImageStorage { get; set; }
        private int Handle { get; set; }
        private XciHeader CardHeader { get; set; }
        private Xci CardImage { get; set; }
        private Keyset Keyset { get; set; }

        public EmulatedGameCard() { }

        public EmulatedGameCard(Keyset keyset)
        {
            Keyset = keyset;
        }
        public GameCardHandle GetGameCardHandle()
        {
            return new GameCardHandle(Handle);
        }

        public bool IsGameCardHandleInvalid(GameCardHandle handle)
        {
            return Handle != handle.Value;
        }

        public bool IsGameCardInserted()
        {
            return CardImageStorage != null;
        }

        public void InsertGameCard(IStorage cardImageStorage)
        {
            RemoveGameCard();

            CardImageStorage = cardImageStorage;

            CardImage = new Xci(Keyset, cardImageStorage);
            CardHeader = CardImage.Header;
        }

        public void RemoveGameCard()
        {
            if (IsGameCardInserted())
            {
                CardImageStorage = null;
                Handle++;
            }
        }
        
        internal Result GetXci(out Xci xci, GameCardHandle handle)
        {
            xci = default;

            if (IsGameCardHandleInvalid(handle)) return ResultFs.InvalidGameCardHandleOnRead.Log();
            if (!IsGameCardInserted()) return ResultFs.GameCardNotInserted.Log();

            xci = CardImage;
            return Result.Success;
        }
        public Result Read(GameCardHandle handle, long offset, Span<byte> destination)
        {
            if (IsGameCardHandleInvalid(handle)) return ResultFs.InvalidGameCardHandleOnRead.Log();
            if (!IsGameCardInserted()) return ResultFs.GameCardNotInserted.Log();

            return CardImageStorage.Read(offset, destination);
        }

        public Result GetGameCardImageHash(Span<byte> outBuffer)
        {
            if (outBuffer.Length < 0x20) return ResultFs.InvalidBufferForGameCard.Log();
            if (!IsGameCardInserted()) return ResultFs.GameCardNotInserted.Log();

            CardHeader.ImageHash.CopyTo(outBuffer.Slice(0, 0x20));
            return Result.Success;
        }

        public Result GetGameCardDeviceId(Span<byte> outBuffer)
        {
            if (outBuffer.Length < 0x10) return ResultFs.InvalidBufferForGameCard.Log();
            if (!IsGameCardInserted()) return ResultFs.GameCardNotInserted.Log();

            // Skip the security mode check

            // Instead of caching the CardKeyArea data, read the value directly
            return CardImageStorage.Read(0x7110, outBuffer.Slice(0, 0x10));
        }

        internal Result GetCardInfo(out GameCardInfo cardInfo, GameCardHandle handle)
        {
            cardInfo = default;

            if (IsGameCardHandleInvalid(handle)) return ResultFs.InvalidGameCardHandleOnGetCardInfo.Log();
            if (!IsGameCardInserted()) return ResultFs.GameCardNotInserted.Log();

            cardInfo = GetCardInfoImpl();
            return Result.Success;
        }

        private GameCardInfo GetCardInfoImpl()
        {
            var info = new GameCardInfo();

            CardHeader.RootPartitionHeaderHash.AsSpan().CopyTo(info.RootPartitionHeaderHash);
            info.PackageId = CardHeader.PackageId;
            info.Size = GameCard.GetGameCardSizeBytes(CardHeader.GameCardSize);
            info.RootPartitionOffset = CardHeader.RootPartitionOffset;
            info.RootPartitionHeaderSize = CardHeader.RootPartitionHeaderSize;
            info.SecureAreaOffset = GameCard.CardPageToOffset(CardHeader.LimAreaPage);
            info.SecureAreaSize = info.Size - info.SecureAreaOffset;
            info.UpdateVersion = CardHeader.UppVersion;
            info.UpdateTitleId = CardHeader.UppId;
            info.Attribute = CardHeader.Flags;

            return info;
        }
    }
}
