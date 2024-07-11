using LibHac.Common;

namespace LibHac.Fs.Impl;

internal readonly struct SaveDataMetaPolicy
{
    internal const int ThumbnailFileSize = 0x40060;

    private readonly SaveDataType _type;

    public SaveDataMetaPolicy(SaveDataType saveType)
    {
        _type = saveType;
    }

    public void GenerateMetaInfo(out SaveDataMetaInfo metaInfo)
    {
        UnsafeHelpers.SkipParamInit(out metaInfo);

        if (_type == SaveDataType.Account || _type == SaveDataType.Device)
        {
            metaInfo.Type = SaveDataMetaType.Thumbnail;
            metaInfo.Size = ThumbnailFileSize;
        }
        else
        {
            metaInfo.Type = SaveDataMetaType.None;
            metaInfo.Size = 0;
        }
    }

    public long GetSaveDataMetaSize()
    {
        GenerateMetaInfo(out SaveDataMetaInfo metaInfo);
        return metaInfo.Size;
    }

    public SaveDataMetaType GetSaveDataMetaType()
    {
        GenerateMetaInfo(out SaveDataMetaInfo metaInfo);
        return metaInfo.Type;
    }
}

internal readonly struct SaveDataMetaPolicyForSaveDataTransfer
{
    private readonly SaveDataMetaPolicy _base;

    public SaveDataMetaPolicyForSaveDataTransfer(SaveDataType saveType) => _base = new SaveDataMetaPolicy(saveType);
    public void GenerateMetaInfo(out SaveDataMetaInfo metaInfo) => _base.GenerateMetaInfo(out metaInfo);
    public long GetSaveDataMetaSize() => _base.GetSaveDataMetaSize();
    public SaveDataMetaType GetSaveDataMetaType() => _base.GetSaveDataMetaType();
}

internal readonly struct SaveDataMetaPolicyForSaveDataTransferVersion2
{
    private readonly SaveDataMetaPolicy _base;

    public SaveDataMetaPolicyForSaveDataTransferVersion2(SaveDataType saveType) => _base = new SaveDataMetaPolicy(saveType);
    public void GenerateMetaInfo(out SaveDataMetaInfo metaInfo) => _base.GenerateMetaInfo(out metaInfo);
    public long GetSaveDataMetaSize() => _base.GetSaveDataMetaSize();
    public SaveDataMetaType GetSaveDataMetaType() => _base.GetSaveDataMetaType();
}