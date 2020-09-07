namespace LibHac.FsSystem.NcaUtils
{
    public class TitleVersion
    {
        public uint Version { get; }
        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }
        public int Revision { get; }

        public TitleVersion(uint version, bool isSystemTitle = false)
        {
            Version = version;

            if (isSystemTitle)
            {
                Revision = (int)(version & ((1 << 16) - 1));
                Patch = (int)((version >> 16) & ((1 << 4) - 1));
                Minor = (int)((version >> 20) & ((1 << 6) - 1));
                Major = (int)((version >> 26) & ((1 << 6) - 1));
            }
            else
            {
                Revision = (byte)version;
                Patch = (byte)(version >> 8);
                Minor = (byte)(version >> 16);
                Major = (byte)(version >> 24);
            }
        }

        public override string ToString()
        {
            return $"{Major}.{Minor}.{Patch}.{Revision}";
        }
    }

    public enum NcaSectionType
    {
        Code,
        Data,
        Logo
    }

    public enum NcaContentType
    {
        Program,
        Meta,
        Control,
        Manual,
        Data,
        PublicData
    }

    public enum DistributionType
    {
        Download,
        GameCard
    }

    public enum NcaEncryptionType
    {
        Auto,
        None,
        XTS,
        AesCtr,
        AesCtrEx
    }

    public enum NcaHashType
    {
        Auto,
        None,
        Sha256,
        Ivfc
    }

    public enum NcaFormatType
    {
        Romfs,
        Pfs0
    }
}
