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
        public IFileSystem ContentFs { get; }
        public IFileSystem SaveFs { get; }

        public Dictionary<string, Nca> Ncas { get; } = new Dictionary<string, Nca>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, SaveDataFileSystem> Saves { get; } = new Dictionary<string, SaveDataFileSystem>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<ulong, Title> Titles { get; } = new Dictionary<ulong, Title>();
        public Dictionary<ulong, Application> Applications { get; } = new Dictionary<ulong, Application>();

        public SwitchFs(Keyset keyset, IFileSystem contentFileSystem, IFileSystem saveFileSystem)
        {
            Keyset = keyset;
            ContentFs = contentFileSystem;
            SaveFs = saveFileSystem;

            OpenAllSaves();
            OpenAllNcas();
            ReadTitles();
            ReadControls();
            CreateApplications();
        }

        public static SwitchFs OpenSdCard(Keyset keyset, IAttributeFileSystem fileSystem)
        {
            var concatFs = new ConcatenationFileSystem(fileSystem);
            var saveDirFs = new SubdirectoryFileSystem(concatFs, "/Nintendo/save");
            var contentDirFs = new SubdirectoryFileSystem(concatFs, "/Nintendo/Contents");

            var encSaveFs = new AesXtsFileSystem(saveDirFs, keyset.SdCardKeys[0], 0x4000);
            var encContentFs = new AesXtsFileSystem(contentDirFs, keyset.SdCardKeys[1], 0x4000);

            return new SwitchFs(keyset, encContentFs, encSaveFs);
        }

        public static SwitchFs OpenNandPartition(Keyset keyset, IAttributeFileSystem fileSystem)
        {
            var concatFs = new ConcatenationFileSystem(fileSystem);
            var saveDirFs = new SubdirectoryFileSystem(concatFs, "/save");
            var contentDirFs = new SubdirectoryFileSystem(concatFs, "/Contents");

            return new SwitchFs(keyset, contentDirFs, saveDirFs);
        }

        public static SwitchFs OpenNcaDirectory(Keyset keyset, IFileSystem fileSystem)
        {
            return new SwitchFs(keyset, fileSystem, null);
        }

        private void OpenAllNcas()
        {
            IEnumerable<DirectoryEntry> files = ContentFs.OpenDirectory("/", OpenDirectoryMode.All).EnumerateEntries("*.nca", SearchOptions.RecurseSubdirectories);

            foreach (DirectoryEntry fileEntry in files)
            {
                Nca nca = null;
                try
                {
                    IStorage storage = ContentFs.OpenFile(fileEntry.FullPath, OpenMode.Read).AsStorage();

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
                    Console.WriteLine($"{ex.Message} File: {fileEntry.FullPath}");
                }

                if (nca?.NcaId != null) Ncas.Add(nca.NcaId, nca);
            }
        }

        private void OpenAllSaves()
        {
            if (SaveFs == null) return;

            foreach (DirectoryEntry fileEntry in SaveFs.EnumerateEntries().Where(x => x.Type == DirectoryEntryType.File))
            {
                SaveDataFileSystem save = null;
                string saveName = Path.GetFileNameWithoutExtension(fileEntry.Name);

                try
                {
                    IFile file = SaveFs.OpenFile(fileEntry.FullPath, OpenMode.Read);
                    save = new SaveDataFileSystem(Keyset, file.AsStorage(), IntegrityCheckLevel.None, true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{ex.Message} File: {fileEntry.FullPath}");
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
                var pfs0 = new PartitionFileSystem(sect);
                IFile file = pfs0.OpenFile(pfs0.Files[0], OpenMode.Read);

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
                var romfs = new RomFsFileSystem(title.ControlNca.OpenSection(0, false, IntegrityCheckLevel.ErrorOnInvalid, true));
                IFile control = romfs.OpenFile("control.nacp", OpenMode.Read);

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
