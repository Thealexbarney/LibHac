using System;
using System.IO;
using System.Linq;
using System.Text;
using libhac;

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

        static void ListTitles(SdFs sdfs)
        {
            foreach (var title in sdfs.Titles.Values.OrderBy(x => x.Id))
            {
                Console.WriteLine($"{title.Name} {title.Control?.Version}");
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
                if (app.Nacp?.BcatSaveDataSize > 0)
                    sb.AppendLine($"BCAT save: {Util.GetBytesReadable(app.Nacp.BcatSaveDataSize)}");

                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
