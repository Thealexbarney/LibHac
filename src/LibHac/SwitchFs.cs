using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using LibHac.IO;
using LibHac.IO.Save;

namespace LibHac
{
    public class SwitchFs : IDisposable
    {
        public Keyset Keyset { get; }
        public IFileSystem Fs { get; }
        public string ContentsDir { get; }
        public string SaveDir { get; }

        public Dictionary<string, Nca> Ncas { get; } = new Dictionary<string, Nca>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, SaveData> Saves { get; } = new Dictionary<string, SaveData>(StringComparer.OrdinalIgnoreCase);
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
                    IStorage storage = OpenSplitNcaStorage(Fs, file);
                    if (storage == null) continue;

                    using (var reader = new BinaryReader(storage.AsStream(), Encoding.Default, true))
                    {
                        reader.BaseStream.Position = 0x20;
                        isNax0 = reader.ReadUInt32() == 0x3058414E; // NAX0
                        reader.BaseStream.Position = 0;
                    }

                    if (isNax0)
                    {
                        string sdPath = "/" + Util.GetRelativePath(file, ContentsDir).Replace('\\', '/');
                        var nax0 = new Nax0(Keyset, storage, sdPath, false);
                        nca = new Nca(Keyset, nax0.BaseStorage, false);
                    }
                    else
                    {
                        nca = new Nca(Keyset, storage, false);
                    }

                    nca.NcaId = Path.GetFileNameWithoutExtension(file);
                    string extension = nca.Header.ContentType == ContentType.Meta ? ".cnmt.nca" : ".nca";
                    nca.Filename = file;
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
                SaveData save = null;
                string saveName = Path.GetFileNameWithoutExtension(file);

                try
                {
                    IStorage storage = Fs.OpenFile(file, FileMode.Open).AsStorage();

                    string sdPath = "/" + Util.GetRelativePath(file, SaveDir).Replace('\\', '/');
                    var nax0 = new Nax0(Keyset, storage, sdPath, false);
                    save = new SaveData(Keyset, nax0.BaseStorage, IntegrityCheckLevel.None, true);
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
                IStorage sect = nca.OpenSection(0, false, IntegrityCheckLevel.ErrorOnInvalid, true);
                var pfs0 = new Pfs(sect);
                IStorage file = pfs0.OpenFile(pfs0.Files[0]);

                var metadata = new Cnmt(file.AsStream());
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
                var romfs = new Romfs(title.ControlNca.OpenSection(0, false, IntegrityCheckLevel.ErrorOnInvalid, true));
                IStorage control = romfs.OpenFile("/control.nacp");

                title.Control = new Nacp(control.AsStream());

                foreach (NacpDescription desc in title.Control.Descriptions)
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

        internal static IStorage OpenSplitNcaStorage(IFileSystem fs, string path)
        {
            var files = new List<string>();
            var storages = new List<IStorage>();

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
                    return fs.OpenFile(path, FileMode.Open, FileAccess.Read).AsStorage();
                }
                files.Add(path);
            }
            else
            {
                throw new FileNotFoundException("Could not find the input file or directory");
            }

            if (files.Count == 1)
            {
                return fs.OpenFile(files[0], FileMode.Open, FileAccess.Read).AsStorage();
            }

            foreach (string file in files)
            {
                storages.Add(fs.OpenFile(file, FileMode.Open, FileAccess.Read).AsStorage());
            }

            if (storages.Count == 0) return null; //todo

            return new ConcatenationStorage(storages, true);
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
            return Ncas.Sum(x => x.Header.NcaSize);
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
