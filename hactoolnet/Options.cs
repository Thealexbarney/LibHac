using LibHac;

namespace hactoolnet
{
    internal class Options
    {
        public bool RunCustom;
        public string InFile;
        public FileType InFileType = FileType.Nca;
        public bool Raw;
        public bool Validate;
        public string Keyfile;
        public string TitleKeyFile;
        public string ConsoleKeyFile;
        public string[] SectionOut = new string[4];
        public string[] SectionOutDir = new string[4];
        public string ExefsOut;
        public string ExefsOutDir;
        public string RomfsOut;
        public string RomfsOutDir;
        public string DebugOutDir;
        public string SaveOutDir;
        public string OutDir;
        public string SdSeed;
        public string NspOut;
        public string SdPath;
        public string BaseNca;
        public string RootDir;
        public string UpdateDir;
        public string NormalDir;
        public string SecureDir;
        public string LogoDir;
        public bool ListApps;
        public bool ListTitles;
        public bool ListRomFs;
        public bool SignSave;
        public ulong TitleId;
    }

    internal enum FileType
    {
        Nca,
        Pfs0,
        Romfs,
        Nax0,
        Xci,
        SwitchFs,
        Save,
        Keygen,
        Pk11
    }

    internal class Context
    {
        public Options Options;
        public Keyset Keyset;
        public IProgressReport Logger;
    }
}
