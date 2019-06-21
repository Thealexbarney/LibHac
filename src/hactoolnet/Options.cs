﻿using LibHac;
using LibHac.Fs;

namespace hactoolnet
{
    internal class Options
    {
        public bool RunCustom;
        public string InFile;
        public FileType InFileType = FileType.Nca;
        public bool Raw;
        public bool Validate;
        public bool UseDevKeys;
        public bool EnableHash;
        public string Keyfile;
        public string TitleKeyFile;
        public string ConsoleKeyFile;
        public string AccessLog;
        public string[] SectionOut = new string[4];
        public string[] SectionOutDir = new string[4];
        public string HeaderOut;
        public string ExefsOut;
        public string ExefsOutDir;
        public string RomfsOut;
        public string RomfsOutDir;
        public string DebugOutDir;
        public string SaveOutDir;
        public string OutDir;
        public string OutFile;
        public string PlaintextOut;
        public string SdSeed;
        public string NspOut;
        public string SdPath;
        public string BaseNca;
        public string BaseFile;
        public string RootDir;
        public string UpdateDir;
        public string NormalDir;
        public string SecureDir;
        public string LogoDir;
        public string ReplaceFileSource;
        public string ReplaceFileDest;
        public string RepackSource;
        public bool ListApps;
        public bool ListTitles;
        public bool ListNcas;
        public bool ListRomFs;
        public bool ListFiles;
        public bool SignSave;
        public bool TrimSave;
        public bool ReadBench;
        public bool BuildHfs;
        public ulong TitleId;
        public string BenchType;

        public IntegrityCheckLevel IntegrityLevel
        {
            get
            {
                if (Validate) return IntegrityCheckLevel.IgnoreOnInvalid;
                if (EnableHash) return IntegrityCheckLevel.ErrorOnInvalid;
                return IntegrityCheckLevel.None;
            }
        }
    }

    internal enum FileType
    {
        Nca,
        Pfs0,
        PfsBuild,
        Nsp,
        Romfs,
        RomfsBuild,
        Nax0,
        Xci,
        SwitchFs,
        Save,
        Keygen,
        Pk11,
        Pk21,
        Kip1,
        Ini1,
        Ndv0,
        Bench
    }

    internal class Context
    {
        public Options Options;
        public Keyset Keyset;
        public ProgressBar Logger;
        public Horizon Horizon;
    }
}
