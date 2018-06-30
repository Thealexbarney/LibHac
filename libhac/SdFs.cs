using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            ReadControls();
        }

        private void OpenAllNcas()
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
                    var extention = nca.Header.ContentType == ContentType.Meta ? ".cnmt.nca" : ".nca";
                    nca.Filename = nca.NcaId + extention;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{ex.Message} {file}");
                }

                if (nca != null) Ncas.Add(nca.NcaId, nca);
            }
        }

        private void ReadTitles()
        {
            foreach (var nca in Ncas.Values.Where(x => x.Header.ContentType == ContentType.Meta))
            {
                var title = new Title();

                // Meta contents always have 1 Partition FS section with 1 file in it
                Stream sect = nca.OpenSection(0, false);
                var pfs0 = new Pfs0(sect);
                var file = pfs0.GetFile(0);

                var metadata = new Cnmt(new MemoryStream(file));
                title.Id = metadata.TitleId;
                title.Version = new TitleVersion(metadata.TitleVersion);
                title.Metadata = metadata;
                title.MetaNca = nca;
                title.Ncas.Add(nca);

                foreach (var content in metadata.ContentEntries)
                {
                    var ncaId = content.NcaId.ToHexString();

                    if (Ncas.TryGetValue(ncaId, out Nca contentNca))
                    {
                        title.Ncas.Add(contentNca);
                    }

                    switch (content.Type)
                    {
                        case CnmtContentType.Program:
                            title.ProgramNca = contentNca;
                            break;
                        case CnmtContentType.Control:
                            title.ControlNca = contentNca;
                            break;
                    }
                }

                Titles.Add(title.Id, title);
            }
        }

        private void ReadControls()
        {
            foreach (var title in Titles.Values.Where(x => x.ControlNca != null))
            {
                var romfs = new Romfs(title.ControlNca.OpenSection(0, false));
                var control = romfs.GetFile("/control.nacp");
                Directory.CreateDirectory("control");
                File.WriteAllBytes($"control/{title.Id:X16}.nacp", control);

                var reader = new BinaryReader(new MemoryStream(control));
                title.Control = new Nacp(reader);

                foreach (var lang in title.Control.Languages)
                {
                    if (!string.IsNullOrWhiteSpace(lang.Title))
                    {
                        title.Name = lang.Title;
                        break;
                    }
                }
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

    [DebuggerDisplay("{" + nameof(Name) + "}")]
    public class Title
    {
        public ulong Id { get; internal set; }
        public TitleVersion Version { get; internal set; }
        public List<Nca> Ncas { get; } = new List<Nca>();
        public Cnmt Metadata { get; internal set; }

        public string Name { get; internal set; }
        public Nacp Control { get; internal set; }
        public Nca MetaNca { get; internal set; }
        public Nca ProgramNca { get; internal set; }
        public Nca ControlNca { get; internal set; }
    }
}
