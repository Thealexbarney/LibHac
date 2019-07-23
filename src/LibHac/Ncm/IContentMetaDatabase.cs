using System;

namespace LibHac.Ncm
{
    public interface IContentMetaDatabase
    {
        Result Set(ContentMetaKey key, ReadOnlySpan<byte> value);
        Result Get(out long valueSize, ContentMetaKey key, Span<byte> valueBuffer);
        Result Remove(ContentMetaKey key);
        Result GetContentIdByType(out ContentId contentId, ContentMetaKey key, ContentType type);
        Result ListContentInfo(out int count, Span<ContentInfo> outInfo, ContentMetaKey key, int startIndex);

        Result List(out int totalEntryCount, out int matchedEntryCount, Span<ContentMetaKey> keys, ContentMetaType type,
            ulong applicationTitleId, ulong minTitleId, ulong maxTitleId, ContentMetaAttribute attributes);

        Result GetLatestContentMetaKey(out ContentMetaKey key, ulong titleId);
        Result ListApplication(out int totalEntryCount, out int matchedEntryCount, Span<ApplicationContentMetaKey> keys, ContentMetaType type);
        Result Has(out bool hasKey, ContentMetaKey key);
        Result HasAll(out bool hasAllKeys, ReadOnlySpan<ContentMetaKey> key);
        Result GetSize(out long size, ContentMetaKey key);
        Result GetRequiredSystemVersion(out int version, ContentMetaKey key);
        Result GetPatchId(out ulong titleId, ContentMetaKey key);
        Result DisableForcibly();
        Result LookupOrphanContent(Span<bool> outOrphaned, ReadOnlySpan<ContentId> contentIds);
        Result Commit();
        Result HasContent(out bool hasContent, ContentMetaKey key, ContentId contentId);
        Result ListContentMetaInfo(out int entryCount, Span<ContentMetaKey> outInfo, ContentMetaKey key, int startIndex);
        Result GetAttributes(out ContentMetaAttribute attributes, ContentMetaKey key);
        Result GetRequiredApplicationVersion(out int version, ContentMetaKey key);
        //Result GetContentIdByTypeAndIdOffset(out ContentId contentId, ContentMetaKey key, ContentType type, byte idOffset);
    }
}