using libhac;

namespace Net
{
    internal class Options
    {
        public string Keyfile;
        public string TitleKeyFile;
        public string ConsoleKeyFile;
        public ulong TitleId;
        public int Version;
        public ulong DeviceId;
        public string CertFile;
    }

    internal class Context
    {
        public Options Options;
        public Keyset Keyset;
        public IProgressReport Logger;
    }
}
