using System.Runtime.InteropServices;

namespace LibHac.Ncm
{
    [StructLayout(LayoutKind.Sequential, Size = 0x18, Pack = 1)]
    public struct ContentInfo
    {
        public ContentId contentId;
        public uint size1;
        public ushort size2;
        private ContentType contentType;
        private byte IdOffset;
    }

    public class ApplicationContentMetaKey
    {
        public ContentMetaKey Key { get; set; }
        public ulong TitleId { get; set; }
    }
}
