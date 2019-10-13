namespace LibHac.Ncm
{
    public enum ContentType : byte
    {
        Meta = 0,
        Program = 1,
        Data = 2,
        Control = 3,
        HtmlDocument = 4,
        LegalInformation = 5,
        DeltaFragment = 6
    }

    public enum ContentMetaType : byte
    {
        SystemProgram = 1,
        SystemData = 2,
        SystemUpdate = 3,
        BootImagePackage = 4,
        BootImagePackageSafe = 5,
        Application = 0x80,
        Patch = 0x81,
        AddOnContent = 0x82,
        Delta = 0x83
    }

    public enum ContentMetaAttribute : byte
    {
        None = 0,
        IncludesExFatDriver = 1,
        Rebootless = 2
    }

    public enum UpdateType : byte
    {
        ApplyAsDelta = 0,
        Overwrite = 1,
        Create = 2
    }
}
