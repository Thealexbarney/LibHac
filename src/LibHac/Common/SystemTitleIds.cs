using LibHac.Ncm;

namespace LibHac.Common
{
    public static class SystemTitleIds
    {
        public static TitleId Fs => new TitleId(0x0100000000000000);
        public static TitleId Loader => new TitleId(0x0100000000000001);
        public static TitleId Ncm => new TitleId(0x0100000000000002);
        public static TitleId ProcessManager => new TitleId(0x0100000000000003);
        public static TitleId Sm => new TitleId(0x0100000000000004);
        public static TitleId Boot => new TitleId(0x0100000000000005);

        public static TitleId Bcat => new TitleId(0x010000000000000C);
    }
}
