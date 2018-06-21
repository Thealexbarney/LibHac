using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace libhac
{
    public class SdFs
    {
        public Keyset Keyset { get; }
        public string RootDir { get; }
        public string ContentsDir { get; }
        public string[] Files { get; }

        public SdFs(Keyset keyset, string sdPath)
        {
            if (Directory.Exists(Path.Combine(sdPath, "Nintendo")))
            {
                RootDir = sdPath;
                Keyset = keyset;
                ContentsDir = Path.Combine(sdPath, "Nintendo", "Contents");
            }

            Files = Directory.GetFiles(ContentsDir, "00", SearchOption.AllDirectories).Select(Path.GetDirectoryName).ToArray();
        }

        public IEnumerable<Nca> ReadAllNca()
        {
            foreach (var file in Files)
            {
                var sdPath = "/" + Util.GetRelativePath(file, ContentsDir).Replace('\\', '/');
                var nax0 = new Nax0(Keyset, file, sdPath);
                var nca = new Nca(Keyset, nax0.Stream);
                nca.Name = Path.GetFileName(file);
                yield return nca;
            }

        }
    }
}
