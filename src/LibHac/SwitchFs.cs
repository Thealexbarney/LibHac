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
        public IDirectory ContentsDir { get; }
        public IDirectory SaveDir { get; }

        public Dictionary<string, Nca> Ncas { get; } = new Dictionary<string, Nca>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, Savefile> Saves { get; } = new Dictionary<string, Savefile>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<ulong, Title> Titles { get; } = new Dictionary<ulong, Title>();
        public Dictionary<ulong, Application> Applications { get; } = new Dictionary<ulong, Application>();

        public SwitchFs(Keyset keyset, IFileSystem fs)
        {
            Fs = fs;
            Keyset = keyset;

            if (fs.GetDirectory("Nintendo").Exists)
            {
                ContentsDir = fs.GetDirectory("Nintendo/Contents");
                SaveDir = fs.GetDirectory("Nintendo/save");
            }
            else
            {
                if (fs.GetDirectory("Contents").Exists)
                {
                    ContentsDir = fs.GetDirectory("Contents");
                }

                if (fs.GetDirectory("save").Exists)
                {
                    SaveDir = fs.GetDirectory("save");
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

            Dictionary<IFileSytemEntry, IStorage> storages = new Dictionary<IFileSytemEntry, IStorage>();

            foreach (IFileSytemEntry nca in ContentsDir.GetFileSystemEntries("*.nca", SearchOption.AllDirectories))
                storages[nca] = OpenSplitNcaStream(nca);      

            foreach (KeyValuePair<IFileSytemEntry, IStorage> kv in storages)
            {
                Nca nca = null;
                IFileSytemEntry file = kv.Key;
                IStorage storage = kv.Value;
                try
                {
                    bool isNax0;
                    if (storage == null) continue;

                    using (var reader = new BinaryReader(storage.AsStream(), Encoding.Default, true))
                    {
                        reader.BaseStream.Position = 0x20;
                        isNax0 = reader.ReadUInt32() == 0x3058414E; // NAX0
                        reader.BaseStream.Position = 0;
                    }

                    if (isNax0)
                    {
                        string sdPath = Util.GetRelativePath(file.Path, ContentsDir.Path).Replace("\\", "/");
                        var nax0 = new Nax0(Keyset, storage, sdPath, false);
                        nca = new Nca(Keyset, nax0.BaseStorage, false);
                    }
                    else
                    {
                        nca = new Nca(Keyset, storage, false);
                    }

                    nca.NcaId = Path.GetFileNameWithoutExtension(file.Path);
                    string extension = nca.Header.ContentType == ContentType.Meta ? ".cnmt.nca" : ".nca";
                    nca.Filename = nca.NcaId + extension;
                }
                catch (MissingKeyException ex)
                {
                    if (ex.Name == null)
                    { Console.WriteLine($"{ex.Message} File:\n{file.Path}"); }
                    else
                    {
                        string name = ex.Type == KeyType.Title ? $"Title key for rights ID {ex.Name}" : ex.Name;
                        Console.WriteLine($"{ex.Message}\nKey: {name}\nFile: {file.Path}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{ex.Message} File: {file.Path}");
                }

                if (nca?.NcaId != null) Ncas.Add(nca.NcaId, nca);
            }
        }

        private void OpenAllSaves()
        {
            if (SaveDir == null) return;

            IFileSytemEntry[] files = Fs.GetFileSystemEntries(SaveDir, "*");

            foreach (IFile file in files)
            {
                Savefile save = null;
                string saveName = Path.GetFileNameWithoutExtension(file.Path);
                IStorage storage = Fs.OpenFile(file, FileMode.Open);
                try
                {
                    string sdPath = Util.GetRelativePath(file.Path, SaveDir.Path).Replace("\\", "/");
                    var nax0 = new Nax0(Keyset, storage, sdPath, false);
                    save = new Savefile(Keyset, nax0.BaseStorage, IntegrityCheckLevel.None, true);
                }
                catch (Exception)
                {
                    try
                    {
                        save = new Savefile(Keyset, storage, IntegrityCheckLevel.None, false);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"{ex.Message} File: {file}");
                    }
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
                IStorage sect;
                try
                {
                    sect = nca.OpenSection(0, false, IntegrityCheckLevel.ErrorOnInvalid, true);
                } catch(Exception e)
                {
                    continue;
                }

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

        internal static IStorage OpenSplitNcaStream(IFileSytemEntry nca)
        {
            var files = new List<IFile>();
            var storages = new List<IStorage>();    

            if (nca.Exists)
            {
                if (typeof(IFile).IsAssignableFrom(nca.GetType())) // if entry is a IFile
                {
                    IFile file = (IFile)nca;
                    if (file.Name != "00")
                        return file.Open(FileMode.Open, FileAccess.Read);
                    
                    files.Add(file);
                }
                else if (typeof(IDirectory).IsAssignableFrom(nca.GetType()))
                {
                    IDirectory directory = (IDirectory)nca;
                    while (true)
                    {
                        IFile partFile = directory.GetFile($"{files.Count:D2}");
                        if (!partFile.Exists) break;

                        files.Add(partFile);
                    }
                }
            }
            else
                throw new FileNotFoundException("Could not find the input file or directory");

            if (files.Count == 1)
                return files[0].Open(FileMode.Open, FileAccess.Read);

            foreach (IFile file in files)
                storages.Add(file.Open( FileMode.Open, FileAccess.Read));

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
