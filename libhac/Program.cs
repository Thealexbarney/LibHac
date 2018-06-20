using System.IO;

namespace libhac
{
    class Program
    {
        static void Main(string[] args)
        {
            var keyset = ExternalKeys.ReadKeyFile(args[0]);
            keyset.SetSdSeed(args[1].ToBytes());

            var nax0 = new Nax0(keyset, args[2], args[3]);
            using (var output = new FileStream(args[4], FileMode.Create))
            using (var progress = new ProgressBar())
            {
                progress.LogMessage($"Writing {args[4]}");
                Util.CopyStream(nax0.Stream, output, nax0.Stream.Length, progress);
            }
        }
    }
}
