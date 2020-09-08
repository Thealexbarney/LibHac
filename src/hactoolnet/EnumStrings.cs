using LibHac;
using LibHac.Fs;
using LibHac.FsSystem.NcaUtils;
using LibHac.Ncm;
using ContentType = LibHac.Ncm.ContentType;

namespace hactoolnet
{
    internal static class EnumStrings
    {
        public static string Print(this ContentType value)
        {
            return value switch
            {
                ContentType.Meta => nameof(ContentType.Meta),
                ContentType.Program => nameof(ContentType.Program),
                ContentType.Data => nameof(ContentType.Data),
                ContentType.Control => nameof(ContentType.Control),
                ContentType.HtmlDocument => nameof(ContentType.HtmlDocument),
                ContentType.LegalInformation => nameof(ContentType.LegalInformation),
                ContentType.DeltaFragment => nameof(ContentType.DeltaFragment),
                _ => value.ToString()
            };
        }

        public static string Print(this ContentMetaType value)
        {
            return value switch
            {
                ContentMetaType.SystemProgram => nameof(ContentMetaType.SystemProgram),
                ContentMetaType.SystemData => nameof(ContentMetaType.SystemData),
                ContentMetaType.SystemUpdate => nameof(ContentMetaType.SystemUpdate),
                ContentMetaType.BootImagePackage => nameof(ContentMetaType.BootImagePackage),
                ContentMetaType.BootImagePackageSafe => nameof(ContentMetaType.BootImagePackageSafe),
                ContentMetaType.Application => nameof(ContentMetaType.Application),
                ContentMetaType.Patch => nameof(ContentMetaType.Patch),
                ContentMetaType.AddOnContent => nameof(ContentMetaType.AddOnContent),
                ContentMetaType.Delta => nameof(ContentMetaType.Delta),
                _ => value.ToString()
            };
        }

        public static string Print(this DistributionType value)
        {
            return value switch
            {
                DistributionType.Download => nameof(DistributionType.Download),
                DistributionType.GameCard => nameof(DistributionType.GameCard),
                _ => value.ToString()
            };
        }

        public static string Print(this NcaContentType value)
        {
            return value switch
            {
                NcaContentType.Program => nameof(NcaContentType.Program),
                NcaContentType.Meta => nameof(NcaContentType.Meta),
                NcaContentType.Control => nameof(NcaContentType.Control),
                NcaContentType.Manual => nameof(NcaContentType.Manual),
                NcaContentType.Data => nameof(NcaContentType.Data),
                NcaContentType.PublicData => nameof(NcaContentType.PublicData),
                _ => value.ToString()
            };
        }

        public static string Print(this NcaFormatType value)
        {
            return value switch
            {
                NcaFormatType.Romfs => nameof(NcaFormatType.Romfs),
                NcaFormatType.Pfs0 => nameof(NcaFormatType.Pfs0),
                _ => value.ToString()
            };
        }

        public static string Print(this XciPartitionType value)
        {
            return value switch
            {
                XciPartitionType.Update => nameof(XciPartitionType.Update),
                XciPartitionType.Normal => nameof(XciPartitionType.Normal),
                XciPartitionType.Secure => nameof(XciPartitionType.Secure),
                XciPartitionType.Logo => nameof(XciPartitionType.Logo),
                XciPartitionType.Root => nameof(XciPartitionType.Root),
                _ => value.ToString()
            };
        }

        public static string Print(this Validity value)
        {
            return value switch
            {
                Validity.Unchecked => nameof(Validity.Unchecked),
                Validity.Invalid => nameof(Validity.Invalid),
                Validity.Valid => nameof(Validity.Valid),
                Validity.MissingKey => nameof(Validity.MissingKey),
                _ => value.ToString()
            };
        }

        public static string Print(this SaveDataType value)
        {
            return value switch
            {
                SaveDataType.System => nameof(SaveDataType.System),
                SaveDataType.Account => nameof(SaveDataType.Account),
                SaveDataType.Bcat => nameof(SaveDataType.Bcat),
                SaveDataType.Device => nameof(SaveDataType.Device),
                SaveDataType.Temporary => nameof(SaveDataType.Temporary),
                SaveDataType.Cache => nameof(SaveDataType.Cache),
                SaveDataType.SystemBcat => nameof(SaveDataType.SystemBcat),
                _ => value.ToString()
            };
        }
    }
}
