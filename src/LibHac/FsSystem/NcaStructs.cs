namespace LibHac.FsSystem;

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