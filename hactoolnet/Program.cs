using System;
using System.Diagnostics;
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

            if (ctx.Options.RunCustom)
            {
                CustomTask(ctx);
                return;
            }

            using (var logger = new ProgressBar())
            {
                ctx.Logger = logger;
                OpenKeyset(ctx);

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

            //ListSdfs(args);
            //FileReadTest(args);
            //ReadNca();
            //ListSdfs(args);
            //ReadNcaSdfs(args);
        }

        private static void ProcessNca(Context ctx)
        {
            using (var file = new FileStream(ctx.Options.InFile, FileMode.Open, FileAccess.Read))
            {
                var nca = new Nca(ctx.Keyset, file, false);

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

                ctx.Logger.LogMessage(nca.Dump());
            }
        }

        private static void ProcessSwitchFs(Context ctx)
        {
            var switchFs = new SdFs(ctx.Keyset, ctx.Options.InFile);

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

                if (title.ProgramNca == null)
                {
                    ctx.Logger.LogMessage($"Could not find main program data for title {id:X16}");
                    return;
                }

                var romfs = new Romfs(title.ProgramNca.OpenSection(1, false));
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
            var homeTitleKeyFile = Path.Combine(home, ".switch", "titlekeys.txt");
            var keyFile = ctx.Options.Keyfile;
            var titleKeyFile = ctx.Options.TitleKeyFile;

            if (keyFile == null && File.Exists(homeKeyFile))
            {
                keyFile = homeKeyFile;
            }

            if (titleKeyFile == null && File.Exists(homeTitleKeyFile))
            {
                titleKeyFile = homeTitleKeyFile;
            }

            ctx.Keyset = ExternalKeys.ReadKeyFile(keyFile, titleKeyFile, ctx.Logger);
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

        private static void ListSdfs(string[] args)
        {
            var sdfs = LoadSdFs(args);

            Console.WriteLine("Listing NCA files");
            ListNcas(sdfs);

            Console.WriteLine("Listing titles");
            ListTitles(sdfs);

            Console.WriteLine("Listing applications");
            ListApplications(sdfs);

            //DecryptNax0(sdfs, "C0628FB07A89E9050BDA258F74868E8D");
            //DecryptTitle(sdfs, 0x010023900AEE0000);
        }

        static void FileReadTest(string[] args)
        {
            var sdfs = LoadSdFs(args);
            var title = sdfs.Titles[0x0100E95004038000];
            var nca = title.ProgramNca;
            var romfsStream = nca.OpenSection(1, false);
            var romfs = new Romfs(romfsStream);
            var file = romfs.OpenFile("/stream/voice/us/127/127390101.nop");

            using (var output = new FileStream("127390101.nop", FileMode.Create))
            {
                file.CopyTo(output);
            }
        }

        static void ReadNca()
        {
            var keyset = ExternalKeys.ReadKeyFile("keys.txt", "titlekeys.txt");
            using (var file = new FileStream("27eeccfe5f6e7637352273bc46ab97e4.nca", FileMode.Open, FileAccess.Read))
            {
                var nca = new Nca(keyset, file, false);
                var romfsStream = nca.OpenSection(1, false);

                var romfs = new Romfs(romfsStream);
                var bfstm = romfs.OpenFile("/Sound/Resource/Stream/Demo149_1_SoundTrack.bfstm");

                using (var progress = new ProgressBar())
                using (var output = new FileStream("Demo149_1_SoundTrack.bfstm", FileMode.Create))
                {
                    var watch = Stopwatch.StartNew();
                    bfstm.CopyStream(output, bfstm.Length, progress);
                    watch.Stop();
                    progress.LogMessage(watch.Elapsed.TotalSeconds.ToString());
                }
            }
        }

        static void ReadNcaSdfs(string[] args)
        {
            var sdfs = LoadSdFs(args);
            var nca = sdfs.Ncas["8EE79C7AB0F16737BC50F049DFDBB595"];
            var romfsStream = nca.OpenSection(1, false);
            var romfs = new Romfs(romfsStream);
        }

        static void DecryptNax0(SdFs sdFs, string name)
        {
            if (!sdFs.Ncas.ContainsKey(name)) return;
            var nca = sdFs.Ncas[name];
            using (var output = new FileStream($"{nca.NcaId}.nca", FileMode.Create))
            using (var progress = new ProgressBar())
            {
                progress.LogMessage($"Title ID: {nca.Header.TitleId:X16}");
                progress.LogMessage($"Writing {nca.NcaId}.nca");
                nca.Stream.Position = 0;
                nca.Stream.CopyStream(output, nca.Stream.Length, progress);
            }
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

        static SdFs LoadSdFs(string[] args)
        {
            var keyset = ExternalKeys.ReadKeyFile(args[0], "titlekeys.txt");
            keyset.SetSdSeed(args[1].ToBytes());
            var sdfs = new SdFs(keyset, args[2]);
            return sdfs;
        }

        static void ListNcas(SdFs sdfs)
        {
            foreach (Nca nca in sdfs.Ncas.Values.OrderBy(x => x.Header.TitleId))
            {
                Console.WriteLine($"{nca.Header.TitleId:X16} {nca.Header.ContentType.ToString().PadRight(10, ' ')} {nca.NcaId}");
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
