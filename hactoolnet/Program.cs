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
            ListSdContents(args);
        }

        static void DecryptNax0(string[] args)
        {
            var keyset = ExternalKeys.ReadKeyFile(args[0]);
            keyset.SetSdSeed(args[1].ToBytes());

            var nax0 = new Nax0(keyset, args[2], args[3]);
            var nca = new Nca(keyset, nax0.Stream);

            using (var output = new FileStream(args[4], FileMode.Create))
            using (var progress = new ProgressBar())
            {
                progress.LogMessage($"Title ID: {nca.Header.TitleId:X16}");
                progress.LogMessage($"Writing {args[4]}");
                nax0.Stream.CopyStream(output, nax0.Stream.Length, progress);
            }
        }

        static void ListSdContents(string[] args)
        {
            var keyset = ExternalKeys.ReadKeyFile(args[0]);
            keyset.SetSdSeed(args[1].ToBytes());
            var sdfs = new SdFs(keyset, args[2]);
            var ncas = sdfs.ReadAllNca().ToArray();

            foreach (var nca in ncas)
            {
                Console.WriteLine($"{nca.Header.TitleId:X16} {nca.Header.ContentType} {nca.Name}");
            }
        }
    }
}
