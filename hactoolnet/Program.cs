using System.IO;
using libhac;

namespace hactoolnet
{
    public static class Program
    {
        static void Main(string[] args)
        {
            var keyset = ExternalKeys.ReadKeyFile(args[0]);
            keyset.SetSdSeed(args[1].ToBytes());

            var nax0 = new Nax0(keyset, args[2], args[3]);
            var nca = new Nca(keyset, nax0.Stream);

            using (var output = new FileStream(args[4], FileMode.Create))
            using (var progress = new ProgressBar())
            {
                progress.LogMessage($"Title ID: {nca.TitleId:X8}");
                progress.LogMessage($"Writing {args[4]}");
                nax0.Stream.CopyStream(output, nax0.Stream.Length, progress);
            }
        }
    }
}
