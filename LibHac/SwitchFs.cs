using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using LibHac.Save;
using LibHac.Streams;

namespace LibHac
{
    public class SwitchFs : IDisposable
    {
        public Keyset Keyset { get; }
        public IFileSystem Fs { get; }
        public string ContentsDir { get; }
        public string SaveDir { get; }

        public Dictionary<string, Nca> Ncas { get; } = new Dictionary<string, Nca>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, Savefile> Saves { get; } = new Dictionary<string, Savefile>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<ulong, Title> Titles { get; } = new Dictionary<ulong, Title>();
        public Dictionary<ulong, Application> Applications { get; } = new Dictionary<ulong, Application>();

        public SwitchFs(Keyset keyset, IFileSystem fs)
        {
            Fs = fs;
            Keyset = keyset;

            if (fs.DirectoryExists("Nintendo"))
            {
                ContentsDir = fs.GetFullPath(Path.Combine("Nintendo", "Contents"));
                SaveDir = fs.GetFullPath(Path.Combine("Nintendo", "save"));
            }
            else
            {
                if (fs.DirectoryExists("Contents"))
                {
                    ContentsDir = fs.GetFullPath("Contents");
                }

                if (fs.DirectoryExists("save"))
                {
                    SaveDir = fs.GetFullPath("save");
                }
            }

            if (ContentsDir == null)
            {
                throw new DirectoryNotFoundException("Could not find \"Contents\" directory");
            }

            OpenAllSaves();
            OpenAllNcas();
            ReadTitles();
            ReadControls();
            CreateApplications();
        }

        private void OpenAllNcas()
        {
            string[] files = Fs.GetFileSystemEntries(ContentsDir, "*.nca", SearchOption.AllDirectories);

            foreach (string file in files)
            {
                Nca nca = null;
                try
                {
                    bool isNax0;
                    Stream stream = OpenSplitNcaStream(Fs, file);
                    if (stream == null) continue;

                    using (var reader = new BinaryReader(stream, Encoding.Default, true))
                    {
                        stream.Position = 0x20;
                        isNax0 = reader.ReadUInt32() == 0x3058414E; // NAX0
                        stream.Position = 0;
                    }

                    if (isNax0)
                    {
                        string sdPath = "/" + Util.GetRelativePath(file, ContentsDir).Replace('\\', '/');
                        var nax0 = new Nax0(Keyset, stream, sdPath, false);
                        nca = new Nca(Keyset, nax0.Stream, false);
                    }
                    else
                    {
                        nca = new Nca(Keyset, stream, false);
                    }

                    nca.NcaId = Path.GetFileNameWithoutExtension(file);
                    string extension = nca.Header.ContentType == ContentType.Meta ? ".cnmt.nca" : ".nca";
                    nca.Filename = nca.NcaId + extension;
                }
                catch (MissingKeyException ex)
                {
                    if (ex.Name == null)
                    { Console.WriteLine($"{ex.Message} File:\n{file}"); }
                    else
                    {
                        string name = ex.Type == KeyType.Title ? $"Title key for rights ID {ex.Name}" : ex.Name;
                        Console.WriteLine($"{ex.Message}\nKey: {name}\nFile: {file}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{ex.Message} File: {file}");
                }

                if (nca?.NcaId != null) Ncas.Add(nca.NcaId, nca);
            }
        }

        private void OpenAllSaves()
        {
            if (SaveDir == null) return;

            string[] files = Fs.GetFileSystemEntries(SaveDir, "*");

            foreach (string file in files)
            {
                Savefile save = null;
                string saveName = Path.GetFileNameWithoutExtension(file);

                try
                {
                    Stream stream = Fs.OpenFile(file, FileMode.Open);

                    string sdPath = "/" + Util.GetRelativePath(file, SaveDir).Replace('\\', '/');
                    var nax0 = new Nax0(Keyset, stream, sdPath, false);
                    save = new Savefile(Keyset, nax0.Stream, IntegrityCheckLevel.None);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{ex.Message} File: {file}");
                }

                if (save != null && saveName != null)
                {
                    Saves[saveName] = save;
                }
            }
        }

        private void ReadTitles()
        {
            foreach (Nca nca in Ncas.Values.Where(x => x.Header.ContentType == ContentType.Meta))
            {
                var title = new Title();

                // Meta contents always have 1 Partition FS section with 1 file in it
                Stream sect = nca.OpenSection(0, false, IntegrityCheckLevel.ErrorOnInvalid);
                var pfs0 = new Pfs(sect);
                Stream file = pfs0.OpenFile(pfs0.Files[0]);

                var metadata = new Cnmt(file);
                title.Id = metadata.TitleId;
                title.Version = metadata.TitleVersion;
                title.Metadata = metadata;
                title.MetaNca = nca;
                title.Ncas.Add(nca);

                foreach (CnmtContentEntry content in metadata.ContentEntries)
                {
                    string ncaId = content.NcaId.ToHexString();

                    if (Ncas.TryGetValue(ncaId, out Nca contentNca))
                    {
                        title.Ncas.Add(contentNca);
                    }

                    switch (content.Type)
                    {
                        case CnmtContentType.Program:
                        case CnmtContentType.Data:
                            title.MainNca = contentNca;
                            break;
                        case CnmtContentType.Control:
                            title.ControlNca = contentNca;
                            break;
                    }
                }

                Titles[title.Id] = title;
            }
        }

        private void ReadControls()
        {
            foreach (Title title in Titles.Values.Where(x => x.ControlNca != null))
            {
                var romfs = new Romfs(title.ControlNca.OpenSection(0, false, IntegrityCheckLevel.ErrorOnInvalid));
                Stream control = romfs.OpenFile("/control.nacp");

                title.Control = new Nacp(control);

                foreach (NacpDescripion desc in title.Control.Descriptions)
                {
                    if (!string.IsNullOrWhiteSpace(desc.Title))
                    {
                        title.Name = desc.Title;
                        break;
                    }
                }
            }
        }

        private void CreateApplications()
        {
            foreach (Title title in Titles.Values.Where(x => x.Metadata.Type >= TitleType.Application))
            {
                Cnmt meta = title.Metadata;
                ulong appId = meta.ApplicationTitleId;

                if (!Applications.TryGetValue(appId, out Application app))
                {
                    app = new Application();
                    Applications.Add(appId, app);
                }

                app.AddTitle(title);
            }

            foreach (Application app in Applications.Values)
            {
                Nca main = app.Main?.MainNca;
                Nca patch = app.Patch?.MainNca;

                if (main != null)
                {
                    patch?.SetBaseNca(main);
                }
            }
        }

        internal static Stream OpenSplitNcaStream(IFileSystem fs, string path)
        {
            var files = new List<string>();
            var streams = new List<Stream>();

            if (fs.DirectoryExists(path))
            {
                while (true)
                {
                    string partName = Path.Combine(path, $"{files.Count:D2}");
                    if (!fs.FileExists(partName)) break;

                    files.Add(partName);
                }
            }
            else if (fs.FileExists(path))
            {
                if (Path.GetFileName(path) != "00")
                {
                    return fs.OpenFile(path, FileMode.Open, FileAccess.Read);
                }
                files.Add(path);
            }
            else
            {
                throw new FileNotFoundException("Could not find the input file or directory");
            }

            foreach (string file in files)
            {
                streams.Add(fs.OpenFile(file, FileMode.Open, FileAccess.Read));
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
        public Nca MainNca { get; internal set; }
        public Nca ControlNca { get; internal set; }

        public long GetSize()
        {
            return Metadata.ContentEntries
                .Where(x => x.Type < CnmtContentType.DeltaFragment)
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
                case TitleType.Delta:
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
                DisplayVersion = Patch.Control?.DisplayVersion ?? "";
                Nacp = Patch.Control;
            }
            else if (Main != null)
            {
                Name = Main.Name;
                Version = Main.Version;
                DisplayVersion = Main.Control?.DisplayVersion ?? "";
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
