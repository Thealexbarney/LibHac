using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace libhac
{
    public class SdFs : IDisposable
    {
        public Keyset Keyset { get; }
        public string RootDir { get; }
        public string ContentsDir { get; }

        public Dictionary<string, Nca> Ncas { get; } = new Dictionary<string, Nca>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<ulong, Title> Titles { get; } = new Dictionary<ulong, Title>();
        public Dictionary<ulong, Application> Applications { get; } = new Dictionary<ulong, Application>();

        public SdFs(Keyset keyset, string rootDir)
        {
            RootDir = rootDir;
            Keyset = keyset;

            if (Directory.Exists(Path.Combine(rootDir, "Nintendo")))
            {
                ContentsDir = Path.Combine(rootDir, "Nintendo", "Contents");
            }
            else if (Directory.Exists(Path.Combine(rootDir, "Contents")))
            {
                ContentsDir = Path.Combine(rootDir, "Contents");
            }

            OpenAllNcas();
            ReadTitles();
            ReadControls();
            CreateApplications();
        }

        private void OpenAllNcas()
        {
            string[] files = Directory.GetFileSystemEntries(ContentsDir, "*.nca", SearchOption.AllDirectories).ToArray();

            foreach (var file in files)
            {
                Nca nca = null;
                try
                {
                    bool isNax0;
                    Stream stream = OpenSplitNcaStream(file);
                    if (stream == null) continue;

                    using (var reader = new BinaryReader(stream, Encoding.Default, true))
                    {
                        stream.Position = 0x20;
                        isNax0 = reader.ReadUInt32() == 0x3058414E; // NAX0
                        stream.Position = 0;
                    }

                    if (isNax0)
                    {
                        var sdPath = "/" + Util.GetRelativePath(file, ContentsDir).Replace('\\', '/');
                        var nax0 = new Nax0(Keyset, stream, sdPath, false);
                        nca = new Nca(Keyset, nax0.Stream, false);
                    }
                    else
                    {
                        nca = new Nca(Keyset, stream, false);
                    }

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
                title.Version = metadata.TitleVersion;
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

        private void CreateApplications()
        {
            foreach (var title in Titles.Values.Where(x => x.Metadata.Type >= TitleType.Application))
            {
                var meta = title.Metadata;
                ulong appId = meta.ApplicationTitleId;

                if (!Applications.TryGetValue(appId, out var app))
                {
                    app = new Application();
                    Applications.Add(appId, app);
                }

                app.AddTitle(title);
            }
        }

        internal static Stream OpenSplitNcaStream(string path)
        {
            List<string> files = new List<string>();
            List<Stream> streams = new List<Stream>();

            if (Directory.Exists(path))
            {
                while (true)
                {
                    var partName = Path.Combine(path, $"{files.Count:D2}");
                    if (!File.Exists(partName)) break;

                    files.Add(partName);
                }
            }
            else if (File.Exists(path))
            {
                if (Path.GetFileName(path) != "00")
                {
                    return new FileStream(path, FileMode.Open, FileAccess.Read);
                }
                files.Add(path);
            }
            else
            {
                throw new FileNotFoundException("Could not find the input file or directory");
            }

            foreach (var file in files)
            {
                streams.Add(new FileStream(file, FileMode.Open, FileAccess.Read));
            }

            if (streams.Count == 0) return null;

            var stream = new CombinationStream(streams);
            return stream;
        }

        private void DisposeNcas()
        {
            foreach (Nca nca in Ncas.Values)
            {
                nca.Dispose();
            }
            Ncas.Clear();
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

        public long GetSize()
        {
            return Metadata.ContentEntries
                .Where(x => x.Type < CnmtContentType.UpdatePatch)
                .Sum(x => x.Size);
        }
    }

    public class Application
    {
        public Title Main { get; private set; }
        public Title Patch { get; private set; }
        public List<Title> AddOnContent { get; } = new List<Title>();

        public ulong TitleId { get; private set; }
        public TitleVersion Version { get; private set; }
        public Nacp Nacp { get; private set; }

        public string Name { get; private set; }
        public string DisplayVersion { get; private set; }

        public void AddTitle(Title title)
        {
            if (TitleId != 0 && title.Metadata.ApplicationTitleId != TitleId)
                throw new InvalidDataException("Title IDs do not match");
            TitleId = title.Metadata.ApplicationTitleId;

            switch (title.Metadata.Type)
            {
                case TitleType.Application:
                    Main = title;
                    break;
                case TitleType.Patch:
                    Patch = title;
                    break;
                case TitleType.AddOnContent:
                    AddOnContent.Add(title);
                    break;
                case TitleType.DeltaTitle:
                    break;
            }

            UpdateInfo();
        }

        private void UpdateInfo()
        {
            if (Patch != null)
            {
                Name = Patch.Name;
                Version = Patch.Version;
                DisplayVersion = Patch.Control?.Version ?? "";
                Nacp = Patch.Control;
            }
            else if (Main != null)
            {
                Name = Main.Name;
                Version = Main.Version;
                DisplayVersion = Main.Control?.Version ?? "";
                Nacp = Main.Control;
            }
            else
            {
                Name = "";
                DisplayVersion = "";
            }
        }
    }
}
