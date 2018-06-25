using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace libhac
{
    public class SdFs : IDisposable
    {
        public Keyset Keyset { get; }
        public string RootDir { get; }
        public string ContentsDir { get; }
        public string[] Files { get; }
        public List<Nca> Ncas { get; } = new List<Nca>();
        private List<Nax0> Nax0s { get; } = new List<Nax0>();

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

        public void OpenAllNcas()
        {
            foreach (var file in Files)
            {
                Nca nca = null;
                try
                {
                    var sdPath = "/" + Util.GetRelativePath(file, ContentsDir).Replace('\\', '/');
                    var nax0 = Nax0.CreateFromPath(Keyset, file, sdPath);
                    Nax0s.Add(nax0);
                    nca = new Nca(Keyset, nax0.Stream, false);
                    nca.Name = Path.GetFileName(file);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{ex.Message} {file}");
                }

                if (nca != null) Ncas.Add(nca);
            }
        }

        private void DisposeNcas()
        {
            foreach (var nca in Ncas)
            {
                nca.Dispose();
            }
            Ncas.Clear();

            foreach (var nax0 in Nax0s)
            {
                nax0.Dispose();
            }
            Nax0s.Clear();
        }

        public void Dispose()
        {
            DisposeNcas();
        }
    }
}
