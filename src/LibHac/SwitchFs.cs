using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.FsSystem.NcaUtils;
using LibHac.FsSystem.Save;
using LibHac.Ncm;

namespace LibHac
{
    public class SwitchFs : IDisposable
    {
        public Keyset Keyset { get; }
        public IFileSystem ContentFs { get; }
        public IFileSystem SaveFs { get; }

        public Dictionary<string, SwitchFsNca> Ncas { get; } = new Dictionary<string, SwitchFsNca>(StringComparer.OrdinalIgnoreCase);
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
            var contentDirFs = new SubdirectoryFileSystem(concatFs, "/Nintendo/Contents");

            AesXtsFileSystem encSaveFs = null;
            if (fileSystem.DirectoryExists("/Nintendo/save"))
            {
                var saveDirFs = new SubdirectoryFileSystem(concatFs, "/Nintendo/save");
                encSaveFs = new AesXtsFileSystem(saveDirFs, keyset.SdCardKeys[0], 0x4000);
            }

            var encContentFs = new AesXtsFileSystem(contentDirFs, keyset.SdCardKeys[1], 0x4000);

            return new SwitchFs(keyset, encContentFs, encSaveFs);
        }

        public static SwitchFs OpenNandPartition(Keyset keyset, IAttributeFileSystem fileSystem)
        {
            var concatFs = new ConcatenationFileSystem(fileSystem);
            IFileSystem saveDirFs = concatFs.DirectoryExists("/save") ? new SubdirectoryFileSystem(concatFs, "/save") : null;
            var contentDirFs = new SubdirectoryFileSystem(concatFs, "/Contents");

            return new SwitchFs(keyset, contentDirFs, saveDirFs);
        }

        public static SwitchFs OpenNcaDirectory(Keyset keyset, IFileSystem fileSystem)
        {
            return new SwitchFs(keyset, fileSystem, null);
        }

        private void OpenAllNcas()
        {
            // Todo: give warning if directories named "*.nca" are found or manually fix the archive bit
            IEnumerable<DirectoryEntryEx> files = ContentFs.EnumerateEntries("*.nca", SearchOptions.RecurseSubdirectories)
                .Where(x => x.Type == DirectoryEntryType.File);

            foreach (DirectoryEntryEx fileEntry in files)
            {
                SwitchFsNca nca = null;
                try
                {
                    ContentFs.OpenFile(out IFile ncaFile, fileEntry.FullPath, OpenMode.Read).ThrowIfFailure();

                    nca = new SwitchFsNca(new Nca(Keyset, ncaFile.AsStorage()));

                    nca.NcaId = GetNcaFilename(fileEntry.Name, nca);
                    string extension = nca.Nca.Header.ContentType == NcaContentType.Meta ? ".cnmt.nca" : ".nca";
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

            foreach (DirectoryEntryEx fileEntry in SaveFs.EnumerateEntries().Where(x => x.Type == DirectoryEntryType.File))
            {
                SaveDataFileSystem save = null;
                string saveName = Path.GetFileNameWithoutExtension(fileEntry.Name);

                try
                {
                    SaveFs.OpenFile(out IFile file, fileEntry.FullPath, OpenMode.Read).ThrowIfFailure();

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
            foreach (SwitchFsNca nca in Ncas.Values.Where(x => x.Nca.Header.ContentType == NcaContentType.Meta))
            {
                try
                {
                    var title = new Title();

                    IFileSystem fs = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.ErrorOnInvalid);
                    string cnmtPath = fs.EnumerateEntries("/", "*.cnmt").Single().FullPath;

                    fs.OpenFile(out IFile file, cnmtPath, OpenMode.Read).ThrowIfFailure();

                    var metadata = new Cnmt(file.AsStream());
                    title.Id = metadata.TitleId;
                    title.Version = metadata.TitleVersion;
                    title.Metadata = metadata;
                    title.MetaNca = nca;
                    title.Ncas.Add(nca);

                    foreach (CnmtContentEntry content in metadata.ContentEntries)
                    {
                        string ncaId = content.NcaId.ToHexString();

                        if (Ncas.TryGetValue(ncaId, out SwitchFsNca contentNca))
                        {
                            title.Ncas.Add(contentNca);
                        }

                        switch (content.Type)
                        {
                            case Ncm.ContentType.Program:
                            case Ncm.ContentType.Data:
                                title.MainNca = contentNca;
                                break;
                            case Ncm.ContentType.Control:
                                title.ControlNca = contentNca;
                                break;
                        }
                    }

                    Titles[title.Id] = title;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{ex.Message} File: {nca.Filename}");
                }
            }
        }

        private void ReadControls()
        {
            foreach (Title title in Titles.Values.Where(x => x.ControlNca != null))
            {
                IFileSystem romfs = title.ControlNca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.ErrorOnInvalid);
                romfs.OpenFile(out IFile control, "control.nacp", OpenMode.Read).ThrowIfFailure();

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
            foreach (Title title in Titles.Values.Where(x => x.Metadata.Type >= ContentMetaType.Application))
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
                SwitchFsNca main = app.Main?.MainNca;
                SwitchFsNca patch = app.Patch?.MainNca;

                if (main != null && patch != null)
                {
                    patch.BaseNca = main.Nca;
                }
            }
        }

        private string GetNcaFilename(string name, SwitchFsNca nca)
        {
            if (nca.Nca.Header.ContentType != NcaContentType.Meta || !name.EndsWith(".cnmt.nca"))
            {
                return Path.GetFileNameWithoutExtension(name);
            }

            return name.Substring(0, name.Length - ".cnmt.nca".Length);
        }

        private void DisposeNcas()
        {
            //foreach (SwitchFsNca nca in Ncas.Values)
            //{
            //    nca.Dispose();
            //}
            Ncas.Clear();
            Titles.Clear();
        }

        public void Dispose()
        {
            DisposeNcas();
        }
    }

    public class SwitchFsNca
    {
        public Nca Nca { get; set; }
        public Nca BaseNca { get; set; }
        public string NcaId { get; set; }
        public string Filename { get; set; }

        public SwitchFsNca(Nca nca)
        {
            Nca = nca;
        }

        public IStorage OpenStorage(int index, IntegrityCheckLevel integrityCheckLevel)
        {
            if (BaseNca != null) return BaseNca.OpenStorageWithPatch(Nca, index, integrityCheckLevel);

            return Nca.OpenStorage(index, integrityCheckLevel);
        }

        public IFileSystem OpenFileSystem(int index, IntegrityCheckLevel integrityCheckLevel)
        {
            if (BaseNca != null) return BaseNca.OpenFileSystemWithPatch(Nca, index, integrityCheckLevel);

            return Nca.OpenFileSystem(index, integrityCheckLevel);
        }

        public IStorage OpenStorage(NcaSectionType type, IntegrityCheckLevel integrityCheckLevel)
        {
            return OpenStorage(Nca.GetSectionIndexFromType(type, Nca.Header.ContentType), integrityCheckLevel);
        }

        public IFileSystem OpenFileSystem(NcaSectionType type, IntegrityCheckLevel integrityCheckLevel)
        {
            return OpenFileSystem(Nca.GetSectionIndexFromType(type, Nca.Header.ContentType), integrityCheckLevel);
        }

        public Validity VerifyNca(IProgressReport logger = null, bool quiet = false)
        {
            if (BaseNca != null)
            {
                return BaseNca.VerifyNca(Nca, logger, quiet);
            }
            else
            {
                return Nca.VerifyNca(logger, quiet);
            }
        }
    }

    [DebuggerDisplay("{" + nameof(Name) + "}")]
    public class Title
    {
        public ulong Id { get; internal set; }
        public TitleVersion Version { get; internal set; }
        public List<SwitchFsNca> Ncas { get; } = new List<SwitchFsNca>();
        public Cnmt Metadata { get; internal set; }

        public string Name { get; internal set; }
        public Nacp Control { get; internal set; }
        public SwitchFsNca MetaNca { get; internal set; }
        public SwitchFsNca MainNca { get; internal set; }
        public SwitchFsNca ControlNca { get; internal set; }

        public long GetSize()
        {
            return Ncas.Sum(x => x.Nca.Header.NcaSize);
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
                case ContentMetaType.Application:
                    Main = title;
                    break;
                case ContentMetaType.Patch:
                    Patch = title;
                    break;
                case ContentMetaType.AddOnContent:
                    AddOnContent.Add(title);
                    break;
                case ContentMetaType.Delta:
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
