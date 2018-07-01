using System.IO;

namespace libhac
{
    public class Nacp
    {
        public NacpLang[] Languages { get; } = new NacpLang[0x10];
        public string Version { get; }

        public Nacp(BinaryReader reader)
        {
            var start = reader.BaseStream.Position;

            for (int i = 0; i < 16; i++)
            {
                Languages[i] = new NacpLang(reader);
            }

            reader.BaseStream.Position = start + 0x3060;
            Version = reader.ReadUtf8Z();
        }
    }

    public class NacpLang
    {
        public string Title { get; }
        public string Developer { get; }

        public NacpLang(BinaryReader reader)
        {
            var start = reader.BaseStream.Position;
            Title = reader.ReadUtf8Z();
            reader.BaseStream.Position = start + 0x200;
            Developer = reader.ReadUtf8Z();
            reader.BaseStream.Position = start + 0x300;
        }
    }
}
