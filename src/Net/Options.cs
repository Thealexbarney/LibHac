using LibHac;

namespace Net
{
    internal class Options
    {
        public ulong TitleId;
        public int Version;
        public ulong DeviceId;
        public string CertFile;
        public string CommonCertFile;
        public string Token;
        public bool GetMetadata;
    }

    internal class Context
    {
        public Options Options;
        public Keyset Keyset;
        public IProgressReport Logger;
    }
}
