using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using LibHac;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.FsSystem.NcaUtils;
using LibHac.FsSystem.Save;
using LibHac.Ns;

namespace hactoolnet
{
    internal static class ProcessSwitchFs
    {
        public static void Process(Context ctx)
        {
            SwitchFs switchFs;
            var baseFs = new LocalFileSystem(ctx.Options.InFile);

            if (Directory.Exists(Path.Combine(ctx.Options.InFile, "Nintendo", "Contents", "registered")))
            {
                ctx.Logger.LogMessage("Treating path as SD card storage");
                switchFs = SwitchFs.OpenSdCard(ctx.KeySet, baseFs);

                CheckForNcaFolders(ctx, switchFs);
            }
            else if (Directory.Exists(Path.Combine(ctx.Options.InFile, "Contents", "registered")))
            {
                ctx.Logger.LogMessage("Treating path as NAND storage");
                switchFs = SwitchFs.OpenNandPartition(ctx.KeySet, baseFs);

                CheckForNcaFolders(ctx, switchFs);
            }
            else
            {
                ctx.Logger.LogMessage("Treating path as a directory of loose NCAs");
                switchFs = SwitchFs.OpenNcaDirectory(ctx.KeySet, baseFs);
            }

            if (ctx.Options.ListNcas)
            {
                ctx.Logger.LogMessage(ListNcas(switchFs));
            }

            if (ctx.Options.ListTitles)
            {
                ctx.Logger.LogMessage(ListTitles(switchFs));
            }

            if (ctx.Options.ListApps)
            {
                ctx.Logger.LogMessage(ListApplications(switchFs));
            }

            if (ctx.Options.ExefsOutDir != null || ctx.Options.ExefsOut != null)
            {
                ulong id = ctx.Options.TitleId;
                if (id == 0)
                {
                    ctx.Logger.LogMessage("Title ID must be specified to dump ExeFS");
                    return;
                }

                if (!switchFs.Titles.TryGetValue(id, out Title title))
                {
                    ctx.Logger.LogMessage($"Could not find title {id:X16}");
                    return;
                }

                if (title.MainNca == null)
                {
                    ctx.Logger.LogMessage($"Could not find main data for title {id:X16}");
                    return;
                }

                if (!title.MainNca.Nca.SectionExists(NcaSectionType.Code))
                {
                    ctx.Logger.LogMessage($"Main NCA for title {id:X16} has no ExeFS section");
                    return;
                }

                if (ctx.Options.ExefsOutDir != null)
                {
                    IFileSystem fs = title.MainNca.OpenFileSystem(NcaSectionType.Code, ctx.Options.IntegrityLevel);
                    fs.Extract(ctx.Options.ExefsOutDir, ctx.Logger);
                }

                if (ctx.Options.ExefsOut != null)
                {
                    title.MainNca.OpenStorage(NcaSectionType.Code, ctx.Options.IntegrityLevel).WriteAllBytes(ctx.Options.ExefsOut, ctx.Logger);
                }
            }

            if (ctx.Options.RomfsOutDir != null || ctx.Options.RomfsOut != null)
            {
                ulong id = ctx.Options.TitleId;
                if (id == 0)
                {
                    ctx.Logger.LogMessage("Title ID must be specified to dump RomFS");
                    return;
                }

                if (!switchFs.Titles.TryGetValue(id, out Title title))
                {
                    ctx.Logger.LogMessage($"Could not find title {id:X16}");
                    return;
                }

                if (title.MainNca == null)
                {
                    ctx.Logger.LogMessage($"Could not find main data for title {id:X16}");
                    return;
                }

                if (!title.MainNca.Nca.SectionExists(NcaSectionType.Data))
                {
                    ctx.Logger.LogMessage($"Main NCA for title {id:X16} has no RomFS section");
                    return;
                }

                ProcessRomfs.Process(ctx, title.MainNca.OpenStorage(NcaSectionType.Data, ctx.Options.IntegrityLevel));
            }

            if (ctx.Options.OutDir != null)
            {
                SaveTitle(ctx, switchFs);
            }

            if (ctx.Options.NspOut != null)
            {
                ProcessPfs.CreateNsp(ctx, switchFs);
            }

            if (ctx.Options.SaveOutDir != null)
            {
                ExportSdSaves(ctx, switchFs);
            }

            if (ctx.Options.Validate)
            {
                ValidateSwitchFs(ctx, switchFs);
            }
        }

        private static void ValidateSwitchFs(Context ctx, SwitchFs switchFs)
        {
            if (ctx.Options.TitleId != 0)
            {
                ulong id = ctx.Options.TitleId;

                if (!switchFs.Titles.TryGetValue(id, out Title title))
                {
                    ctx.Logger.LogMessage($"Could not find title {id:X16}");
                    return;
                }

                ValidateTitle(ctx, title, "");

                return;
            }

            foreach (Application app in switchFs.Applications.Values)
            {
                ctx.Logger.LogMessage($"Checking {app.Name}...");

                Title mainTitle = app.Patch ?? app.Main;

                if (mainTitle != null)
                {
                    ValidateTitle(ctx, mainTitle, "Main title");
                }

                foreach (Title title in app.AddOnContent)
                {
                    ValidateTitle(ctx, title, "Add-on content");
                }
            }
        }

        private static void ValidateTitle(Context ctx, Title title, string caption)
        {
            try
            {
                ctx.Logger.LogMessage($"  {caption} {title.Id:x16}");

                foreach (SwitchFsNca nca in title.Ncas)
                {
                    ctx.Logger.LogMessage($"    {nca.Nca.Header.ContentType.Print()}");

                    Validity validity = nca.VerifyNca(ctx.Logger, true);

                    ctx.Logger.LogMessage($"      {validity.Print()}");
                }
            }
            catch (Exception ex)
            {
                ctx.Logger.LogMessage($"Error processing title {title.Id:x16}:\n{ex.Message}");
            }
        }

        private static void SaveTitle(Context ctx, SwitchFs switchFs)
        {
            ulong id = ctx.Options.TitleId;
            if (id == 0)
            {
                ctx.Logger.LogMessage("Title ID must be specified to save title");
                return;
            }

            if (!switchFs.Titles.TryGetValue(id, out Title title))
            {
                ctx.Logger.LogMessage($"Could not find title {id:X16}");
                return;
            }

            string saveDir = Path.Combine(ctx.Options.OutDir, $"{title.Id:X16}v{title.Version.Version}");
            Directory.CreateDirectory(saveDir);

            foreach (SwitchFsNca nca in title.Ncas)
            {
                Stream stream = nca.Nca.BaseStorage.AsStream();
                string outFile = Path.Combine(saveDir, nca.Filename);
                ctx.Logger.LogMessage(nca.Filename);
                using (var outStream = new FileStream(outFile, FileMode.Create, FileAccess.ReadWrite))
                {
                    stream.CopyStream(outStream, stream.Length, ctx.Logger);
                }
            }
        }

        static string ListTitles(SwitchFs sdfs)
        {
            var table = new TableBuilder("Title ID", "Version", "", "Type", "Size", "Display Version", "Name");

            foreach (Title title in sdfs.Titles.Values.OrderBy(x => x.Id))
            {
                table.AddRow($"{title.Id:X16}",
                    $"v{title.Version?.Version}",
                    title.Version?.ToString(),
                    title.Metadata?.Type.Print(),
                    Utilities.GetBytesReadable(title.GetSize()),
                    title.Control.Value.DisplayVersion.ToString(),
                    title.Name);
            }

            return table.Print();
        }

        static string ListNcas(SwitchFs sdfs)
        {
            var table = new TableBuilder("NCA ID", "Type", "Title ID");

            foreach (SwitchFsNca nca in sdfs.Ncas.Values.OrderBy(x => x.NcaId))
            {
                table.AddRow(nca.NcaId, nca.Nca.Header.ContentType.Print(), nca.Nca.Header.TitleId.ToString("X16"));
            }

            return table.Print();
        }

        static string ListApplications(SwitchFs sdfs)
        {
            var sb = new StringBuilder();

            foreach (Application app in sdfs.Applications.Values.OrderBy(x => x.Name))
            {
                if (app.Main != null)
                {
                    sb.AppendLine($"{app.Name} v{app.DisplayVersion} ({app.Main.Id.ToString("X16")})");
                    sb.AppendLine($"Software: {Utilities.GetBytesReadable(app.Main.GetSize())}");
                }
                else
                {
                    sb.AppendLine($"{app.Name} v{app.DisplayVersion}");
                }

                if (app.Patch != null)
                {
                    sb.AppendLine($"Update Data: {Utilities.GetBytesReadable(app.Patch.GetSize())}");
                }

                if (app.AddOnContent.Count > 0)
                {
                    sb.AppendLine($"DLC: {Utilities.GetBytesReadable(app.AddOnContent.Sum(x => x.GetSize()))}");
                }

                ref ApplicationControlProperty nacp = ref app.Nacp.Value;

                long userTotalSaveDataSize = nacp.UserAccountSaveDataSize + nacp.UserAccountSaveDataJournalSize;
                long deviceTotalSaveDataSize = nacp.DeviceSaveDataSize + nacp.DeviceSaveDataJournalSize;

                if (userTotalSaveDataSize > 0)
                    sb.AppendLine($"User save: {Utilities.GetBytesReadable(userTotalSaveDataSize)}");
                if (deviceTotalSaveDataSize > 0)
                    sb.AppendLine($"System save: {Utilities.GetBytesReadable(deviceTotalSaveDataSize)}");
                if (nacp.BcatDeliveryCacheStorageSize > 0)
                    sb.AppendLine($"BCAT save: {Utilities.GetBytesReadable(nacp.BcatDeliveryCacheStorageSize)}");

                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static void ExportSdSaves(Context ctx, SwitchFs switchFs)
        {
            foreach (KeyValuePair<string, SaveDataFileSystem> save in switchFs.Saves)
            {
                string outDir = Path.Combine(ctx.Options.SaveOutDir, save.Key);
                save.Value.Extract(outDir, ctx.Logger);
            }
        }

        private static void CheckForNcaFolders(Context ctx, SwitchFs switchFs)
        {
            // Skip this until Linux gets FAT attribute support
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

            IFileSystem fs = switchFs.ContentFs;

            DirectoryEntryEx[] ncaDirs = fs.EnumerateEntries("*.nca", SearchOptions.RecurseSubdirectories)
                .Where(x => x.Type == DirectoryEntryType.Directory)
                .Where(x => fs.FileExists($"{x.FullPath}/00"))
                .ToArray();

            if (ncaDirs.Length > 0)
            {
                ctx.Logger.LogMessage("Warning: NCA folders without the archive flag were found. Fixing...");
            }

            foreach (DirectoryEntryEx file in ncaDirs)
            {
                fs.SetConcatenationFileAttribute(file.FullPath);
                ctx.Logger.LogMessage($"{file.FullPath}");
            }
        }
    }
}
