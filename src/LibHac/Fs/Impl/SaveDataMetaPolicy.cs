using LibHac.Common;

namespace LibHac.Fs.Impl
{
    internal readonly struct SaveDataMetaPolicy
    {
        private readonly SaveDataType _type;
        private const int ThumbnailFileSize = 0x40060;

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
}
