using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Ncm;
using LibHac.Ns;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using LibHac.Tools.FsSystem.Save;
using LibHac.Tools.Ncm;
using LibHac.Util;
using KeyType = LibHac.Common.Keys.KeyType;

namespace LibHac.Tools.Fs;

public class SwitchFs : IDisposable
{
    public KeySet KeySet { get; }
    public IFileSystem ContentFs { get; }
    public IFileSystem SaveFs { get; }

    public Dictionary<string, SwitchFsNca> Ncas { get; } = new Dictionary<string, SwitchFsNca>(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, SaveDataFileSystem> Saves { get; } = new Dictionary<string, SaveDataFileSystem>(StringComparer.OrdinalIgnoreCase);
    public Dictionary<ulong, Title> Titles { get; } = new Dictionary<ulong, Title>();
    public Dictionary<ulong, Application> Applications { get; } = new Dictionary<ulong, Application>();

    public SwitchFs(KeySet keySet, IFileSystem contentFileSystem, IFileSystem saveFileSystem)
    {
        KeySet = keySet;
        ContentFs = contentFileSystem;
        SaveFs = saveFileSystem;

        OpenAllSaves();
        OpenAllNcas();
        ReadTitles();
        ReadControls();
        CreateApplications();
    }

    public static SwitchFs OpenSdCard(KeySet keySet, ref UniqueRef<IAttributeFileSystem> fileSystem)
    {
        var concatFs = new ConcatenationFileSystem(ref fileSystem);

        using var contentDirPath = new LibHac.Fs.Path();
        PathFunctions.SetUpFixedPath(ref contentDirPath.Ref(), "/Nintendo/Contents"u8).ThrowIfFailure();

        using var saveDirPath = new LibHac.Fs.Path();
        PathFunctions.SetUpFixedPath(ref saveDirPath.Ref(), "/Nintendo/save"u8).ThrowIfFailure();

        var contentDirFs = new SubdirectoryFileSystem(concatFs);
        contentDirFs.Initialize(in contentDirPath).ThrowIfFailure();

        AesXtsFileSystem encSaveFs = null;
        if (concatFs.DirectoryExists("/Nintendo/save"))
        {
            var saveDirFs = new SubdirectoryFileSystem(concatFs);
            saveDirFs.Initialize(in saveDirPath).ThrowIfFailure();

            encSaveFs = new AesXtsFileSystem(saveDirFs, keySet.SdCardEncryptionKeys[0].DataRo.ToArray(), 0x4000);
        }

        var encContentFs = new AesXtsFileSystem(contentDirFs, keySet.SdCardEncryptionKeys[1].DataRo.ToArray(), 0x4000);

        return new SwitchFs(keySet, encContentFs, encSaveFs);
    }

    public static SwitchFs OpenNandPartition(KeySet keySet, ref UniqueRef<IAttributeFileSystem> fileSystem)
    {
        var concatFs = new ConcatenationFileSystem(ref fileSystem);
        SubdirectoryFileSystem saveDirFs = null;
        SubdirectoryFileSystem contentDirFs;

        if (concatFs.DirectoryExists("/save"))
        {
            using var savePath = new LibHac.Fs.Path();
            PathFunctions.SetUpFixedPath(ref savePath.Ref(), "/save"u8);

            saveDirFs = new SubdirectoryFileSystem(concatFs);
            saveDirFs.Initialize(in savePath).ThrowIfFailure();
        }

        using var contentsPath = new LibHac.Fs.Path();
        PathFunctions.SetUpFixedPath(ref contentsPath.Ref(), "/Contents"u8);

        contentDirFs = new SubdirectoryFileSystem(concatFs);
        contentDirFs.Initialize(in contentsPath).ThrowIfFailure();

        return new SwitchFs(keySet, contentDirFs, saveDirFs);
    }

    public static SwitchFs OpenNcaDirectory(KeySet keySet, IFileSystem fileSystem)
    {
        return new SwitchFs(keySet, fileSystem, null);
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
                using var ncaFile = new UniqueRef<IFile>();
                ContentFs.OpenFile(ref ncaFile.Ref, fileEntry.FullPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();

                nca = new SwitchFsNca(new Nca(KeySet, ncaFile.Release().AsStorage()));

                nca.NcaId = GetNcaFilename(fileEntry.Name, nca);
                string extension = nca.Nca.Header.ContentType == NcaContentType.Meta ? ".cnmt.nca" : ".nca";
                nca.Filename = nca.NcaId + extension;
            }
            catch (MissingKeyException ex)
            {
                if (ex.Name == null)
                {
                    Console.WriteLine($"{ex.Message} File:\n{fileEntry}");
                }
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
            string saveName = System.IO.Path.GetFileNameWithoutExtension(fileEntry.Name);

            try
            {
                using var file = new UniqueRef<IFile>();
                SaveFs.OpenFile(ref file.Ref, fileEntry.FullPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();

                save = new SaveDataFileSystem(KeySet, file.Release().AsStorage(), IntegrityCheckLevel.None, true);
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

                using var file = new UniqueRef<IFile>();
                fs.OpenFile(ref file.Ref, cnmtPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();

                var metadata = new Cnmt(file.Release().AsStream());
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
                        case LibHac.Ncm.ContentType.Program:
                        case LibHac.Ncm.ContentType.Data:
                            title.MainNca = contentNca;
                            break;
                        case LibHac.Ncm.ContentType.Control:
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

            using (var control = new UniqueRef<IFile>())
            {
                romfs.OpenFile(ref control.Ref, "/control.nacp"u8, OpenMode.Read).ThrowIfFailure();

                control.Get.Read(out _, 0, title.Control.ByteSpan).ThrowIfFailure();
            }

            foreach (ref readonly ApplicationControlProperty.ApplicationTitle desc in title.Control.Value.Title.ItemsRo)
            {
                if (!desc.NameString.IsEmpty())
                {
                    title.Name = desc.NameString.ToString();
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
            return System.IO.Path.GetFileNameWithoutExtension(name);
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
    public BlitStruct<ApplicationControlProperty> Control { get; } = new BlitStruct<ApplicationControlProperty>(1);
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
    public BlitStruct<ApplicationControlProperty> Nacp { get; private set; } = new BlitStruct<ApplicationControlProperty>(1);

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
            DisplayVersion = Patch.Control.Value.DisplayVersionString.ToString();
            Nacp = Patch.Control;
        }
        else if (Main != null)
        {
            Name = Main.Name;
            Version = Main.Version;
            DisplayVersion = Main.Control.Value.DisplayVersionString.ToString();
            Nacp = Main.Control;
        }
        else
        {
            Name = "";
            DisplayVersion = "";
        }
    }
}