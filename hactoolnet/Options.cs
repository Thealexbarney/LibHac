using libhac;

namespace hactoolnet
{
    internal class Options
    {
        public string InFile;
        public FileType InFileType = FileType.Nca;
        public bool Raw = false;
        public string Keyfile;
        public string TitleKeyFile;
        public string[] SectionOut = new string[4];
        public string[] SectionOutDir = new string[4];
        public string ExefsOut;
        public string ExefsOutDir;
        public string RomfsOut;
        public string RomfsOutDir;
        public string OutDir;
        public string SdSeed;
        public string SdPath;
        public bool ListApps;
        public bool ListTitles;
        public bool ListRomFs;
        public ulong TitleId;

    }

    internal enum FileType
    {
        Nca,
        Pfs0,
        Romfs,
        Nax0,
        SwitchFs
    }

    internal class Context
    {
        public Options Options;
        public Keyset Keyset;
        public IProgressReport Logger;
    }
}
