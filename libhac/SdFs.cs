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

        public Dictionary<string, Nca> Ncas { get; } = new Dictionary<string, Nca>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<ulong, Title> Titles { get; } = new Dictionary<ulong, Title>();

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
            OpenAllNcas();
            ReadTitles();
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
                    nca.NcaId = Path.GetFileNameWithoutExtension(file);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{ex.Message} {file}");
                }

                if (nca != null) Ncas.Add(nca.NcaId, nca);
            }
        }

        public void ReadTitles()
        {
            foreach (var nca in Ncas.Values.Where(x => x.Header.ContentType == ContentType.Meta))
            {
                var title = new Title();

                // Meta contents always have 1 Partition FS section with 1 file in it
                Stream sect = nca.OpenSection(0);
                var pfs0 = new Pfs0(sect);
                var file = pfs0.GetFile(0);

                var metadata = new Cnmt(new MemoryStream(file));
                title.Id = metadata.TitleId;
                title.Version = new TitleVersion(metadata.TitleVersion);
                title.Metadata = metadata;
                Titles.Add(title.Id, title);
            }
        }

        private void DisposeNcas()
        {
            foreach (Nca nca in Ncas.Values)
            {
                nca.Dispose();
            }
            Ncas.Clear();

            // Disposing the Nca disposes the Nax0 as well
            Nax0s.Clear();
            Titles.Clear();
        }

        public void Dispose()
        {
            DisposeNcas();
        }
    }

    public class Title
    {
        public ulong Id { get; internal set; }
        public TitleVersion Version { get; internal set; }
        public List<Nca> Ncas { get; } = new List<Nca>();
        public Cnmt Metadata { get; internal set; }
    }
}
