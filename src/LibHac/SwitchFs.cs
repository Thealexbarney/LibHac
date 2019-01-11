using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LibHac.IO;
using LibHac.IO.Save;

namespace LibHac
{
    public class SwitchFs : IDisposable
    {
        public Keyset Keyset { get; }
        public IAttributeFileSystem BaseFs { get; }
        public AesXtsFileSystem Fs { get; }

        public Dictionary<string, Nca> Ncas { get; } = new Dictionary<string, Nca>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, SaveDataFileSystem> Saves { get; } = new Dictionary<string, SaveDataFileSystem>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<ulong, Title> Titles { get; } = new Dictionary<ulong, Title>();
        public Dictionary<ulong, Application> Applications { get; } = new Dictionary<ulong, Application>();

        public SwitchFs(Keyset keyset, IAttributeFileSystem fs)
        {
            BaseFs = fs;
            Keyset = keyset;

            var concatFs = new ConcatenationFileSystem(BaseFs);
            Fs = new AesXtsFileSystem(concatFs, keyset.SdCardKeys[1], 0x4000);

           // OpenAllSaves();
            OpenAllNcas();
            ReadTitles();
            ReadControls();
            CreateApplications();
        }

        private void OpenAllNcas()
        {
            IEnumerable<DirectoryEntry> files = Fs.OpenDirectory("/", OpenDirectoryMode.All).EnumerateEntries("*.nca", SearchOptions.RecurseSubdirectories);

            foreach (DirectoryEntry fileEntry in files)
            {
                Nca nca = null;
                try
                {
                    var storage = new FileStorage(Fs.OpenFile(fileEntry.FullPath, OpenMode.Read));

                    nca = new Nca(Keyset, storage, false);

                    nca.NcaId = Path.GetFileNameWithoutExtension(fileEntry.Name);
                    string extension = nca.Header.ContentType == ContentType.Meta ? ".cnmt.nca" : ".nca";
                    nca.Filename = nca.NcaId + extension;
                }
                catch (MissingKeyException ex)
                {
                    if (ex.Name == null)
                    { Console.WriteLine($"{ex.Message} File:\n{fileEntry}"); }
                    else
                    {
                        string name = ex.Type == KeyType.Title ? $"Title key for rights ID {ex.Name}" : ex.Name;
                        Console.WriteLine($"{ex.Message}\nKey: {name}\nFile: {fileEntry}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{ex.Message} File: {fileEntry}");
                }

                if (nca?.NcaId != null) Ncas.Add(nca.NcaId, nca);
            }
        }

        //private void OpenAllSaves()
        //{
        //    if (SaveDir == null) return;

        //    string[] files = Fs.GetFileSystemEntries(SaveDir, "*");

        //    foreach (string file in files)
        //    {
        //        SaveDataFileSystem save = null;
        //        string saveName = Path.GetFileNameWithoutExtension(file);

        //        try
        //        {
        //            IStorage storage = Fs.OpenFile(file, FileMode.Open).AsStorage();

        //            string sdPath = "/" + Util.GetRelativePath(file, SaveDir).Replace('\\', '/');
        //            var nax0 = new Nax0(Keyset, storage, sdPath, false);
        //            save = new SaveDataFileSystem(Keyset, nax0.BaseStorage, IntegrityCheckLevel.None, true);
        //        }
        //        catch (Exception ex)
        //        {
        //            Console.WriteLine($"{ex.Message} File: {file}");
        //        }

        //        if (save != null && saveName != null)
        //        {
        //            Saves[saveName] = save;
        //        }
        //    }
        //}

        private void ReadTitles()
        {
            foreach (Nca nca in Ncas.Values.Where(x => x.Header.ContentType == ContentType.Meta))
            {
                var title = new Title();

                // Meta contents always have 1 Partition FS section with 1 file in it
                IStorage sect = nca.OpenSection(0, false, IntegrityCheckLevel.ErrorOnInvalid, true);
                var pfs0 = new PartitionFileSystem(sect);
                IFile file = pfs0.OpenFile(pfs0.Files[0], OpenMode.Read);

                var metadata = new Cnmt(new FileStorage(file).AsStream());
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
                var romfs = new RomFsFileSystem(title.ControlNca.OpenSection(0, false, IntegrityCheckLevel.ErrorOnInvalid, true));
                IStorage control = new FileStorage(romfs.OpenFile("control.nacp", OpenMode.Read));

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
