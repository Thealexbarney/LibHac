using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using libhac;
using libhac.Savefile;

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

                if (ctx.Options.RomfsOutDir != null)
                {
                    NcaSection section = nca.Sections.FirstOrDefault(x => x.Type == SectionType.Romfs || x.Type == SectionType.Bktr);

                    if (section == null)
                    {
                        ctx.Logger.LogMessage("NCA has no RomFS section");
                        return;
                    }

                    if (section.Type == SectionType.Bktr)
                    {
                        if (ctx.Options.BaseNca == null)
                        {
                            ctx.Logger.LogMessage("Cannot save BKTR section without base RomFS");
                            return;
                        }

                        var bktr = nca.OpenSection(1, false);
                        var romfs = new Romfs(bktr);
                        romfs.Extract(ctx.Options.RomfsOutDir, ctx.Logger);

                    }
                    else
                    {
                        var romfs = new Romfs(nca.OpenSection(section.SectionNum, false));
                        romfs.Extract(ctx.Options.RomfsOutDir, ctx.Logger);
                    }
                }

                ctx.Logger.LogMessage(nca.Dump());
            }
        }

        private static void ProcessSwitchFs(Context ctx)
        {
            var switchFs = new SdFs(ctx.Keyset, new FileSystem(ctx.Options.InFile));

            if (ctx.Options.ListTitles)
            {
                ListTitles(switchFs);
            }

            if (ctx.Options.ListApps)
            {
                ctx.Logger.LogMessage(ListApplications(switchFs));
            }

            if (ctx.Options.RomfsOutDir != null)
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
                    ctx.Logger.LogMessage($"Main NCA for title {id:X16} has no Rom FS section");
                    return;
                }

                var romfs = new Romfs(title.MainNca.OpenSection(section.SectionNum, false));
                romfs.Extract(ctx.Options.RomfsOutDir, ctx.Logger);
            }

            if (ctx.Options.OutDir != null)
            {
                SaveTitle(ctx, switchFs);
            }

            if (ctx.Options.NspOut != null)
            {
                CreateNsp(ctx, switchFs);
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

                if (ctx.Options.RomfsOutDir != null)
                {
                    if (xci.SecurePartition == null)
                    {
                        ctx.Logger.LogMessage("Could not find secure partition");
                        return;
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

                    var romfs = new Romfs(mainNca.OpenSection(romfsSection.SectionNum, false));
                    romfs.Extract(ctx.Options.RomfsOutDir, ctx.Logger);
                }
            }
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
                var layout = save.Header.Layout;

                if (ctx.Options.OutDir != null)
                {
                    save.Extract(ctx.Options.OutDir, ctx.Logger);
                }

                if (ctx.Options.DebugOutDir != null)
                {
                    var dir = ctx.Options.DebugOutDir;
                    Directory.CreateDirectory(dir);

                    File.WriteAllBytes(Path.Combine(dir, "L0_0_DuplexL1A"), save.DuplexL1A);
                    File.WriteAllBytes(Path.Combine(dir, "L0_1_DuplexL1B"), save.DuplexL1B);
                    File.WriteAllBytes(Path.Combine(dir, "L0_2_DuplexDataA"), save.DuplexDataA);
                    File.WriteAllBytes(Path.Combine(dir, "L0_3_DuplexDataB"), save.DuplexDataB);

                    save.FileRemap.Position = layout.JournalDataOffset;
                    using (var outFile = new FileStream(Path.Combine(dir, "L0_4_JournalData"), FileMode.Create, FileAccess.Write))
                    {
                        save.FileRemap.CopyStream(outFile, layout.JournalDataSizeB + layout.SizeReservedArea);
                    }

                    File.WriteAllBytes(Path.Combine(dir, "L1_0_JournalTable"), save.JournalTable);
                    File.WriteAllBytes(Path.Combine(dir, "L1_1_JournalBitmapUpdatedPhysical"), save.JournalBitmapUpdatedPhysical);
                    File.WriteAllBytes(Path.Combine(dir, "L1_2_JournalBitmapUpdatedVirtual"), save.JournalBitmapUpdatedVirtual);
                    File.WriteAllBytes(Path.Combine(dir, "L1_3_JournalBitmapUnassigned"), save.JournalBitmapUnassigned);
                    File.WriteAllBytes(Path.Combine(dir, "L1_4_Layer1Hash"), save.JournalLayer1Hash);
                    File.WriteAllBytes(Path.Combine(dir, "L1_5_Layer2Hash"), save.JournalLayer2Hash);
                    File.WriteAllBytes(Path.Combine(dir, "L1_6_Layer3Hash"), save.JournalLayer3Hash);
                    File.WriteAllBytes(Path.Combine(dir, "L1_7_FAT"), save.JournalFat);

                    save.JournalStream.Position = 0;
                    using (var outFile = new FileStream(Path.Combine(dir, "L2_0_SaveData"), FileMode.Create, FileAccess.Write))
                    {
                        save.JournalStream.CopyStream(outFile, save.JournalStream.Length);
                    }
                }
            }
        }

        // For running random stuff
        // ReSharper disable once UnusedParameter.Local
        private static void CustomTask(Context ctx)
        {

        }

        private static void SaveTitle(Context ctx, SdFs switchFs)
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
                nca.Stream.Position = 0;
                var outFile = Path.Combine(saveDir, nca.Filename);
                ctx.Logger.LogMessage(nca.Filename);
                using (var outStream = new FileStream(outFile, FileMode.Create, FileAccess.ReadWrite))
                {
                    nca.Stream.CopyStream(outStream, nca.Stream.Length, ctx.Logger);
                }
            }
        }

        private static void CreateNsp(Context ctx, SdFs switchFs)
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
                builder.AddFile(nca.Filename, nca.Stream);
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

        static void ListTitles(SdFs sdfs)
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

        static string ListApplications(SdFs sdfs)
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
    }
}

