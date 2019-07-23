using System;
using LibHac.Fs;

namespace LibHac.Ncm
{
    public interface IContentStorage
    {
        Result GeneratePlaceHolderId(out PlaceHolderId placeHolderId);
        Result CreatePlaceHolder(PlaceHolderId placeHolderId, ContentId contentId, long fileSize);
        Result DeletePlaceHolder(PlaceHolderId placeHolderId);
        Result HasPlaceHolder(out bool hasPlaceHolder, PlaceHolderId placeHolderId);
        Result WritePlaceHolder(PlaceHolderId placeHolderId, long offset, ReadOnlySpan<byte> buffer);
        Result Register(PlaceHolderId placeHolderId, ContentId contentId);
        Result Delete(ContentId contentId);
        Result Has(out bool hasContent, ContentId contentId);
        Result GetPath(Span<byte> outPath, ContentId contentId);
        Result GetPlaceHolderPath(Span<byte> outPath, PlaceHolderId placeHolderId);
        Result CleanupAllPlaceHolder();
        Result ListPlaceHolder(out int count, Span<PlaceHolderId> placeHolderIds);
        Result GetContentCount(out int count);
        Result ListContentId(out int count, Span<ContentId> contentIds, int startOffset);
        Result GetSizeFromContentId(out long size, ContentId contentId);
        Result DisableForcibly();
        Result RevertToPlaceHolder(PlaceHolderId placeHolderId, ContentId oldContentId, ContentId newContentId);
        Result SetPlaceHolderSize(PlaceHolderId placeHolderId, long size);
        Result ReadContentIdFile(Span<byte> buffer, long size, ContentId contentId, long offset);
        Result GetRightsIdFromPlaceHolderId(out RightsId rightsId, out byte keyGeneration, PlaceHolderId placeHolderId);
        Result GetRightsIdFromContentId(out RightsId rightsId, out byte keyGeneration, ContentId contentId);
        Result WriteContentForDebug(ContentId contentId, long offset, ReadOnlySpan<byte> buffer);
        Result GetFreeSpaceSize(out long size);
        Result GetTotalSpaceSize(out long size);
        Result FlushPlaceHolder();
        //Result GetSizeFromPlaceHolderId(out long size, PlaceHolderId placeHolderId);
        //Result RepairInvalidFileAttribute();
        //Result GetRightsIdFromPlaceHolderIdWithCache(out RightsId rightsId, out byte keyGeneration, PlaceHolderId placeHolderId, out ContentId cacheContentId);
    }
}