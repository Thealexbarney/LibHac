using System;
using System.IO;
using System.Linq;
using libhac;

namespace hactoolnet
{
    public static class Program
    {
        static void Main(string[] args)
        {
            var sdfs = LoadSdFs(args);

            Console.WriteLine("Listing NCA files");
            ListNcas(sdfs);

            Console.WriteLine("Listing titles");
            ListTitles(sdfs);

            //DecryptNax0(sdfs, "C0628FB07A89E9050BDA258F74868E8D");
            DecryptTitle(sdfs, 0x0100000000010800);
        }

        static void DecryptNax0(SdFs sdFs, string name)
        {
            if (!sdFs.Ncas.ContainsKey(name)) return;
            var nca = sdFs.Ncas[name];
            using (var output = new FileStream($"{nca.NcaId}.nca", FileMode.Create))
            using (var progress = new ProgressBar())
            {
                progress.LogMessage($"Title ID: {nca.Header.TitleId:X16}");
                progress.LogMessage($"Writing {nca.NcaId}.nca");
                nca.Stream.Position = 0;
                nca.Stream.CopyStream(output, nca.Stream.Length, progress);
            }
        }

        static void DecryptTitle(SdFs sdFs, ulong titleId)
        {
            var title = sdFs.Titles[titleId];
            var dirName = $"{titleId:X16}v{title.Version.Version}";

            Directory.CreateDirectory(dirName);

            foreach (var nca in title.Ncas)
            {
                using (var output = new FileStream(Path.Combine(dirName, nca.Filename), FileMode.Create))
                using (var progress = new ProgressBar())
                {
                    progress.LogMessage($"Writing {nca.Filename}");
                    nca.Stream.Position = 0;
                    nca.Stream.CopyStream(output, nca.Stream.Length, progress);
                }
            }
        }

        static SdFs LoadSdFs(string[] args)
        {
            var keyset = ExternalKeys.ReadKeyFile(args[0]);
            keyset.SetSdSeed(args[1].ToBytes());
            var sdfs = new SdFs(keyset, args[2]);
            return sdfs;
        }

        static void ListNcas(SdFs sdfs)
        {
            foreach (Nca nca in sdfs.Ncas.Values.OrderBy(x => x.Header.TitleId))
            {
                Console.WriteLine($"{nca.Header.TitleId:X16} {nca.Header.ContentType.ToString().PadRight(10, ' ')} {nca.NcaId}");
            }
        }

        static void ListTitles(SdFs sdfs)
        {
            foreach (var title in sdfs.Titles.Values.OrderBy(x => x.Id))
            {
                Console.WriteLine($"{title.Id:X16} v{title.Version.Version} ({title.Version}) {title.Metadata.Type}");

                foreach (var content in title.Metadata.ContentEntries)
                {
                    Console.WriteLine(
                        $"    {content.NcaId.ToHexString()}.nca {content.Type} {Util.GetBytesReadable(content.Size)}");
                }

                Console.WriteLine("");
            }
        }
    }
}
