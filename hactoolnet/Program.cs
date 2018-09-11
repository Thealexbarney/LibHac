using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using LibHac;
using LibHac.Savefile;

namespace hactoolnet
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            var ctx = new Context();
            ctx.Options = CliParser.Parse(args);
            if (ctx.Options == null) return;

            using (var logger = new ProgressBar())
            {
                ctx.Logger = logger;
                OpenKeyset(ctx);

                if (ctx.Options.RunCustom)
                {
                    CustomTask(ctx);
                    return;
                }

                switch (ctx.Options.InFileType)
                {
                    case FileType.Nca:
                        ProcessNca(ctx);
                        break;
                    case FileType.Pfs0:
                        break;
                    case FileType.Romfs:
                        ProcessRomFs(ctx);
                        break;
                    case FileType.Nax0:
                        break;
                    case FileType.SwitchFs:
                        ProcessSwitchFs(ctx);
                        break;
                    case FileType.Save:
                        ProcessSave(ctx);
                        break;
                    case FileType.Xci:
                        ProcessXci(ctx);
                        break;
                    case FileType.Keygen:
                        ProcessKeygen(ctx);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private static void ProcessNca(Context ctx)
        {
            using (var file = new FileStream(ctx.Options.InFile, FileMode.Open, FileAccess.Read))
            {
                var nca = new Nca(ctx.Keyset, file, false);

                if (ctx.Options.BaseNca != null)
                {
                    var baseFile = new FileStream(ctx.Options.BaseNca, FileMode.Open, FileAccess.Read);
                    var baseNca = new Nca(ctx.Keyset, baseFile, false);
                    nca.SetBaseNca(baseNca);
                }

                for (int i = 0; i < 3; i++)
                {
                    if (ctx.Options.SectionOut[i] != null)
                    {
                        nca.ExportSection(i, ctx.Options.SectionOut[i], ctx.Options.Raw, ctx.Logger);
                    }

                    if (ctx.Options.SectionOutDir[i] != null)
                    {
                        nca.ExtractSection(i, ctx.Options.SectionOutDir[i], ctx.Logger);
                    }

                    if (ctx.Options.Validate && nca.Sections[i] != null)
                    {
                        nca.VerifySection(i, ctx.Logger);
                    }
                }

                if (ctx.Options.ListRomFs && nca.Sections[1] != null)
                {
                    var romfs = new Romfs(nca.OpenSection(1, false));

                    foreach (var romfsFile in romfs.Files)
                    {
                        ctx.Logger.LogMessage(romfsFile.FullPath);
                    }
                }

                if (ctx.Options.RomfsOutDir != null || ctx.Options.RomfsOut != null)
                {
                    NcaSection section = nca.Sections.FirstOrDefault(x => x?.Type == SectionType.Romfs || x?.Type == SectionType.Bktr);

                    if (section == null)
                    {
                        ctx.Logger.LogMessage("NCA has no RomFS section");
                        return;
                    }

                    if (section.Type == SectionType.Bktr && ctx.Options.BaseNca == null)
                    {
                        ctx.Logger.LogMessage("Cannot save BKTR section without base RomFS");
                        return;
                    }

                    if (ctx.Options.RomfsOut != null)
                    {
                        nca.ExportSection(section.SectionNum, ctx.Options.RomfsOut, ctx.Options.Raw, ctx.Logger);
                    }

                    if (ctx.Options.RomfsOutDir != null)
                    {
                        var romfs = new Romfs(nca.OpenSection(section.SectionNum, false));
                        romfs.Extract(ctx.Options.RomfsOutDir, ctx.Logger);
                    }
                }

                if (ctx.Options.ExefsOutDir != null || ctx.Options.ExefsOut != null)
                {
                    NcaSection section = nca.Sections.FirstOrDefault(x => x.IsExefs);

                    if (section == null)
                    {
                        ctx.Logger.LogMessage("Could not find an ExeFS section");
                        return;
                    }

                    if (ctx.Options.ExefsOut != null)
                    {
                        nca.ExportSection(section.SectionNum, ctx.Options.ExefsOut, ctx.Options.Raw, ctx.Logger);
                    }

                    if (ctx.Options.ExefsOutDir != null)
                    {
                        nca.ExtractSection(section.SectionNum, ctx.Options.ExefsOutDir, ctx.Logger);
                    }
                }

                ctx.Logger.LogMessage(nca.Dump());
            }
        }

        private static void ProcessSwitchFs(Context ctx)
        {
            var switchFs = new SwitchFs(ctx.Keyset, new FileSystem(ctx.Options.InFile));

            if (ctx.Options.ListTitles)
            {
                ListTitles(switchFs);
            }

            if (ctx.Options.ListApps)
            {
                ctx.Logger.LogMessage(ListApplications(switchFs));
            }

            if (ctx.Options.ExefsOutDir != null || ctx.Options.ExefsOut != null)
            {
                var id = ctx.Options.TitleId;
                if (id == 0)
                {
                    ctx.Logger.LogMessage("Title ID must be specified to dump ExeFS");
                    return;
                }

                if (!switchFs.Titles.TryGetValue(id, out var title))
                {
                    ctx.Logger.LogMessage($"Could not find title {id:X16}");
                    return;
                }

                if (title.MainNca == null)
                {
                    ctx.Logger.LogMessage($"Could not find main data for title {id:X16}");
                    return;
                }

                var section = title.MainNca.Sections.FirstOrDefault(x => x.IsExefs);

                if (section == null)
                {
                    ctx.Logger.LogMessage($"Main NCA for title {id:X16} has no ExeFS section");
                    return;
                }

                if (ctx.Options.ExefsOutDir != null)
                {
                    title.MainNca.ExtractSection(section.SectionNum, ctx.Options.ExefsOutDir, ctx.Logger);
                }

                if (ctx.Options.ExefsOut != null)
                {
                    title.MainNca.ExportSection(section.SectionNum, ctx.Options.ExefsOut, ctx.Options.Raw, ctx.Logger);
                }
            }

            if (ctx.Options.RomfsOutDir != null || ctx.Options.RomfsOut != null)
            {
                var id = ctx.Options.TitleId;
                if (id == 0)
                {
                    ctx.Logger.LogMessage("Title ID must be specified to dump RomFS");
                    return;
                }

                if (!switchFs.Titles.TryGetValue(id, out var title))
                {
                    ctx.Logger.LogMessage($"Could not find title {id:X16}");
                    return;
                }

                if (title.MainNca == null)
                {
                    ctx.Logger.LogMessage($"Could not find main data for title {id:X16}");
                    return;
                }

                var section = title.MainNca.Sections.FirstOrDefault(x => x?.Type == SectionType.Romfs || x?.Type == SectionType.Bktr);

                if (section == null)
                {
                    ctx.Logger.LogMessage($"Main NCA for title {id:X16} has no RomFS section");
                    return;
                }

                if (ctx.Options.RomfsOutDir != null)
                {
                    var romfs = new Romfs(title.MainNca.OpenSection(section.SectionNum, false));
                    romfs.Extract(ctx.Options.RomfsOutDir, ctx.Logger);
                }

                if (ctx.Options.RomfsOut != null)
                {
                    title.MainNca.ExportSection(section.SectionNum, ctx.Options.RomfsOut, ctx.Options.Raw, ctx.Logger);
                }
            }

            if (ctx.Options.OutDir != null)
            {
                SaveTitle(ctx, switchFs);
            }

            if (ctx.Options.NspOut != null)
            {
                CreateNsp(ctx, switchFs);
            }

            if (ctx.Options.SaveOutDir != null)
            {
                ExportSdSaves(ctx, switchFs);
            }
        }

        private static void ProcessXci(Context ctx)
        {
            using (var file = new FileStream(ctx.Options.InFile, FileMode.Open, FileAccess.Read))
            {
                var xci = new Xci(ctx.Keyset, file);

                if (ctx.Options.RootDir != null)
                {
                    xci.RootPartition?.Extract(ctx.Options.RootDir, ctx.Logger);
                }

                if (ctx.Options.UpdateDir != null)
                {
                    xci.UpdatePartition?.Extract(ctx.Options.UpdateDir, ctx.Logger);
                }

                if (ctx.Options.NormalDir != null)
                {
                    xci.NormalPartition?.Extract(ctx.Options.NormalDir, ctx.Logger);
                }

                if (ctx.Options.SecureDir != null)
                {
                    xci.SecurePartition?.Extract(ctx.Options.SecureDir, ctx.Logger);
                }

                if (ctx.Options.LogoDir != null)
                {
                    xci.LogoPartition?.Extract(ctx.Options.LogoDir, ctx.Logger);
                }

                if (ctx.Options.OutDir != null && xci.RootPartition != null)
                {
                    var root = xci.RootPartition;
                    if (root == null)
                    {
                        ctx.Logger.LogMessage("Could not find root partition");
                        return;
                    }

                    foreach (var sub in root.Files)
                    {
                        var subPfs = new Pfs(root.OpenFile(sub));
                        var subDir = Path.Combine(ctx.Options.OutDir, sub.Name);

                        subPfs.Extract(subDir, ctx.Logger);
                    }
                }

                if (ctx.Options.ExefsOutDir != null || ctx.Options.ExefsOut != null)
                {
                    var mainNca = GetXciMainNca(xci, ctx);

                    if (mainNca == null)
                    {
                        ctx.Logger.LogMessage("Could not find Program NCA");
                        return;
                    }

                    var exefsSection = mainNca.Sections.FirstOrDefault(x => x.IsExefs);

                    if (exefsSection == null)
                    {
                        ctx.Logger.LogMessage("NCA has no ExeFS section");
                        return;
                    }

                    if (ctx.Options.ExefsOutDir != null)
                    {
                        mainNca.ExtractSection(exefsSection.SectionNum, ctx.Options.ExefsOutDir, ctx.Logger);
                    }

                    if (ctx.Options.ExefsOut != null)
                    {
                        mainNca.ExportSection(exefsSection.SectionNum, ctx.Options.ExefsOut, ctx.Options.Raw, ctx.Logger);
                    }
                }

                if (ctx.Options.RomfsOutDir != null || ctx.Options.RomfsOut != null)
                {
                    var mainNca = GetXciMainNca(xci, ctx);

                    if (mainNca == null)
                    {
                        ctx.Logger.LogMessage("Could not find Program NCA");
                        return;
                    }

                    var romfsSection = mainNca.Sections.FirstOrDefault(x => x.Type == SectionType.Romfs);

                    if (romfsSection == null)
                    {
                        ctx.Logger.LogMessage("NCA has no RomFS section");
                        return;
                    }

                    if (ctx.Options.RomfsOutDir != null)
                    {
                        var romfs = new Romfs(mainNca.OpenSection(romfsSection.SectionNum, false));
                        romfs.Extract(ctx.Options.RomfsOutDir, ctx.Logger);
                    }

                    if (ctx.Options.RomfsOut != null)
                    {
                        mainNca.ExportSection(romfsSection.SectionNum, ctx.Options.RomfsOut, ctx.Options.Raw, ctx.Logger);
                    }
                }
            }
        }

        private static void ProcessRomFs(Context ctx)
        {
            using (var file = new FileStream(ctx.Options.InFile, FileMode.Open, FileAccess.Read))
            {
                var romfs = new Romfs(file);
                ProcessRomFs(ctx, romfs);
            }
        }

        private static void ProcessRomFs(Context ctx, Romfs romfs)
        {
            if (ctx.Options.ListRomFs)
            {
                foreach (var romfsFile in romfs.Files)
                {
                    ctx.Logger.LogMessage(romfsFile.FullPath);
                }
            }

            if (ctx.Options.RomfsOut != null)
            {
                using (var outFile = new FileStream(ctx.Options.RomfsOut, FileMode.Create, FileAccess.ReadWrite))
                {
                    var romfsStream = romfs.OpenRawStream();
                    romfsStream.CopyStream(outFile, romfsStream.Length, ctx.Logger);
                }
            }

            if (ctx.Options.RomfsOutDir != null)
            {
                romfs.Extract(ctx.Options.RomfsOutDir, ctx.Logger);
            }
        }

        private static Nca GetXciMainNca(Xci xci, Context ctx)
        {
            if (xci.SecurePartition == null)
            {
                ctx.Logger.LogMessage("Could not find secure partition");
                return null;
            }

            Nca mainNca = null;

            foreach (var fileEntry in xci.SecurePartition.Files.Where(x => x.Name.EndsWith(".nca")))
            {
                var ncaStream = xci.SecurePartition.OpenFile(fileEntry);
                var nca = new Nca(ctx.Keyset, ncaStream, true);

                if (nca.Header.ContentType == ContentType.Program)
                {
                    mainNca = nca;
                }
            }

            return mainNca;
        }

        private static void OpenKeyset(Context ctx)
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var homeKeyFile = Path.Combine(home, ".switch", "prod.keys");
            var homeTitleKeyFile = Path.Combine(home, ".switch", "title.keys");
            var homeConsoleKeyFile = Path.Combine(home, ".switch", "console.keys");
            var keyFile = ctx.Options.Keyfile;
            var titleKeyFile = ctx.Options.TitleKeyFile;
            var consoleKeyFile = ctx.Options.ConsoleKeyFile;

            if (keyFile == null && File.Exists(homeKeyFile))
            {
                keyFile = homeKeyFile;
            }

            if (titleKeyFile == null && File.Exists(homeTitleKeyFile))
            {
                titleKeyFile = homeTitleKeyFile;
            }

            if (consoleKeyFile == null && File.Exists(homeConsoleKeyFile))
            {
                consoleKeyFile = homeConsoleKeyFile;
            }

            ctx.Keyset = ExternalKeys.ReadKeyFile(keyFile, titleKeyFile, consoleKeyFile, ctx.Logger);
            if (ctx.Options.SdSeed != null)
            {
                ctx.Keyset.SetSdSeed(ctx.Options.SdSeed.ToBytes());
            }
        }

        private static void ProcessSave(Context ctx)
        {
            using (var file = new FileStream(ctx.Options.InFile, FileMode.Open, FileAccess.Read))
            {
                var save = new Savefile(file, ctx.Logger);

                if (ctx.Options.OutDir != null)
                {
                    save.Extract(ctx.Options.OutDir, ctx.Logger);
                }

                if (ctx.Options.DebugOutDir != null)
                {
                    var dir = ctx.Options.DebugOutDir;
                    Directory.CreateDirectory(dir);

                    File.WriteAllBytes(Path.Combine(dir, "L0_0_MasterHashA"), save.Header.MasterHashA);
                    File.WriteAllBytes(Path.Combine(dir, "L0_1_MasterHashB"), save.Header.MasterHashB);
                    File.WriteAllBytes(Path.Combine(dir, "L0_2_DuplexMasterA"), save.Header.DuplexMasterA);
                    File.WriteAllBytes(Path.Combine(dir, "L0_3_DuplexMasterB"), save.Header.DuplexMasterB);
                    save.DuplexL1A.WriteAllBytes(Path.Combine(dir, "L0_4_DuplexL1A"), ctx.Logger);
                    save.DuplexL1B.WriteAllBytes(Path.Combine(dir, "L0_5_DuplexL1B"), ctx.Logger);
                    save.DuplexDataA.WriteAllBytes(Path.Combine(dir, "L0_6_DuplexDataA"), ctx.Logger);
                    save.DuplexDataB.WriteAllBytes(Path.Combine(dir, "L0_7_DuplexDataB"), ctx.Logger);
                    save.JournalData.WriteAllBytes(Path.Combine(dir, "L0_9_JournalData"), ctx.Logger);

                    save.DuplexData.WriteAllBytes(Path.Combine(dir, "L1_0_DuplexData"), ctx.Logger);
                    save.JournalTable.WriteAllBytes(Path.Combine(dir, "L2_0_JournalTable"), ctx.Logger);
                    save.JournalBitmapUpdatedPhysical.WriteAllBytes(Path.Combine(dir, "L2_1_JournalBitmapUpdatedPhysical"), ctx.Logger);
                    save.JournalBitmapUpdatedVirtual.WriteAllBytes(Path.Combine(dir, "L2_2_JournalBitmapUpdatedVirtual"), ctx.Logger);
                    save.JournalBitmapUnassigned.WriteAllBytes(Path.Combine(dir, "L2_3_JournalBitmapUnassigned"), ctx.Logger);
                    save.JournalLayer1Hash.WriteAllBytes(Path.Combine(dir, "L2_4_Layer1Hash"), ctx.Logger);
                    save.JournalLayer2Hash.WriteAllBytes(Path.Combine(dir, "L2_5_Layer2Hash"), ctx.Logger);
                    save.JournalLayer3Hash.WriteAllBytes(Path.Combine(dir, "L2_6_Layer3Hash"), ctx.Logger);
                    save.JournalFat.WriteAllBytes(Path.Combine(dir, "L2_7_FAT"), ctx.Logger);

                    save.JournalStreamSource.CreateStream().WriteAllBytes(Path.Combine(dir, "L3_0_SaveData"), ctx.Logger);
                }
            }
        }

        private static void ProcessKeygen(Context ctx)
        {
            Console.WriteLine(ExternalKeys.PrintKeys(ctx.Keyset));
        }

        // For running random stuff
        // ReSharper disable once UnusedParameter.Local
        private static void CustomTask(Context ctx)
        {

        }

        private static void SaveTitle(Context ctx, SwitchFs switchFs)
        {
            var id = ctx.Options.TitleId;
            if (id == 0)
            {
                ctx.Logger.LogMessage("Title ID must be specified to save title");
                return;
            }

            if (!switchFs.Titles.TryGetValue(id, out var title))
            {
                ctx.Logger.LogMessage($"Could not find title {id:X16}");
                return;
            }

            var saveDir = Path.Combine(ctx.Options.OutDir, $"{title.Id:X16}v{title.Version.Version}");
            Directory.CreateDirectory(saveDir);

            foreach (var nca in title.Ncas)
            {
                var stream = nca.GetStream();
                var outFile = Path.Combine(saveDir, nca.Filename);
                ctx.Logger.LogMessage(nca.Filename);
                using (var outStream = new FileStream(outFile, FileMode.Create, FileAccess.ReadWrite))
                {
                    stream.CopyStream(outStream, stream.Length, ctx.Logger);
                }
            }
        }

        private static void CreateNsp(Context ctx, SwitchFs switchFs)
        {
            var id = ctx.Options.TitleId;
            if (id == 0)
            {
                ctx.Logger.LogMessage("Title ID must be specified to save title");
                return;
            }

            if (!switchFs.Titles.TryGetValue(id, out var title))
            {
                ctx.Logger.LogMessage($"Could not find title {id:X16}");
                return;
            }

            var builder = new Pfs0Builder();

            foreach (var nca in title.Ncas)
            {
                builder.AddFile(nca.Filename, nca.GetStream());
            }

            var ticket = new Ticket
            {
                SignatureType = TicketSigType.Rsa2048Sha256,
                Signature = new byte[0x200],
                Issuer = "Root-CA00000003-XS00000020",
                FormatVersion = 2,
                RightsId = title.MainNca.Header.RightsId,
                TitleKeyBlock = title.MainNca.TitleKey,
                CryptoType = title.MainNca.Header.CryptoType2,
                SectHeaderOffset = 0x2C0
            };
            var ticketBytes = ticket.GetBytes();
            builder.AddFile($"{ticket.RightsId.ToHexString()}.tik", new MemoryStream(ticketBytes));

            var thisAssembly = Assembly.GetExecutingAssembly();
            var cert = thisAssembly.GetManifestResourceStream("hactoolnet.CA00000003_XS00000020");
            builder.AddFile($"{ticket.RightsId.ToHexString()}.cert", cert);


            using (var outStream = new FileStream(ctx.Options.NspOut, FileMode.Create, FileAccess.ReadWrite))
            {
                builder.Build(outStream, ctx.Logger);
            }
        }

        static void ListTitles(SwitchFs sdfs)
        {
            foreach (var title in sdfs.Titles.Values.OrderBy(x => x.Id))
            {
                Console.WriteLine($"{title.Name} {title.Control?.DisplayVersion}");
                Console.WriteLine($"{title.Id:X16} v{title.Version.Version} ({title.Version}) {title.Metadata.Type}");

                foreach (var content in title.Metadata.ContentEntries)
                {
                    Console.WriteLine(
                        $"    {content.NcaId.ToHexString()}.nca {content.Type} {Util.GetBytesReadable(content.Size)}");
                }

                foreach (var nca in title.Ncas)
                {
                    Console.WriteLine($"      {nca.HasRightsId} {nca.NcaId} {nca.Header.ContentType}");

                    foreach (var sect in nca.Sections.Where(x => x != null))
                    {
                        Console.WriteLine($"        {sect.SectionNum} {sect.Type} {sect.Header.CryptType} {sect.SuperblockHashValidity}");
                    }
                }

                Console.WriteLine("");
            }
        }

        static string ListApplications(SwitchFs sdfs)
        {
            var sb = new StringBuilder();

            foreach (var app in sdfs.Applications.Values.OrderBy(x => x.Name))
            {
                sb.AppendLine($"{app.Name} v{app.DisplayVersion}");

                if (app.Main != null)
                {
                    sb.AppendLine($"Software: {Util.GetBytesReadable(app.Main.GetSize())}");
                }

                if (app.Patch != null)
                {
                    sb.AppendLine($"Update Data: {Util.GetBytesReadable(app.Patch.GetSize())}");
                }

                if (app.AddOnContent.Count > 0)
                {
                    sb.AppendLine($"DLC: {Util.GetBytesReadable(app.AddOnContent.Sum(x => x.GetSize()))}");
                }

                if (app.Nacp?.UserTotalSaveDataSize > 0)
                    sb.AppendLine($"User save: {Util.GetBytesReadable(app.Nacp.UserTotalSaveDataSize)}");
                if (app.Nacp?.DeviceTotalSaveDataSize > 0)
                    sb.AppendLine($"System save: {Util.GetBytesReadable(app.Nacp.DeviceTotalSaveDataSize)}");
                if (app.Nacp?.BcatDeliveryCacheStorageSize > 0)
                    sb.AppendLine($"BCAT save: {Util.GetBytesReadable(app.Nacp.BcatDeliveryCacheStorageSize)}");

                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static void ExportSdSaves(Context ctx, SwitchFs switchFs)
        {
            foreach (var save in switchFs.Saves)
            {
                var outDir = Path.Combine(ctx.Options.SaveOutDir, save.Key);
                save.Value.Extract(outDir, ctx.Logger);
            }
        }
    }
}

