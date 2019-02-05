using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using LibHac;

namespace Net
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
                ProcessNet(ctx);
            }
        }

        private static void ProcessNet(Context ctx)
        {
            if (ctx.Options.DeviceId == 0)
            {
                CliParser.PrintWithUsage("A non-zero Device ID must be set.");
                return;
            }

            if (ctx.Options.GetMetadata)
            {
                GetMetadata(new NetContext(ctx), ctx.Logger);
                return;
            }

            if (ctx.Options.TitleId == 0)
            {
                CliParser.PrintWithUsage("A non-zero Title ID must be set.");
                return;
            }

            if (string.IsNullOrWhiteSpace(ctx.Options.Token))
            {
                CliParser.PrintWithUsage("A token must be set.");
                return;
            }

            ulong tid = ctx.Options.TitleId;
            int ver = ctx.Options.Version;

            var net = new NetContext(ctx);
            var cnmt = net.GetCnmt(tid, ver);
            if (cnmt == null) return;
            ctx.Logger.LogMessage($"Title is of type {cnmt.Type} and has {cnmt.ContentEntries.Length} content entries");
            var control = net.GetControl(tid, ver);
            if (control != null)
            {
                ctx.Logger.LogMessage($"Title has name {control.Descriptions[0].Title}");
            }
            foreach (var entry in cnmt.ContentEntries)
            {
                ctx.Logger.LogMessage($"{entry.NcaId.ToHexString()} {entry.Type}");
                net.GetNcaFile(tid, ver, entry.NcaId.ToHexString());
            }
        }

        private static void GetMetadata(NetContext net, IProgressReport logger = null)
        {
            VersionList versionList = net.GetVersionList();
            net.Db.ImportVersionList(versionList);
            //net.Db.ImportList("titles.txt");
            net.Save();
            ReadMetaNcas(net, logger);
            
            net.Save();
        }

        private static void ReadMetaNcas(NetContext net, IProgressReport logger = null)
        {
            foreach (TitleMetadata title in net.Db.Titles.Values.ToArray())
            {
                if (title.Versions.Count == 0)
                {
                    int version = 0;
                    if ((title.Id & 0x800) != 0)
                    {
                        version = 1 << 16;
                    }

                    title.Versions.Add(version, new TitleVersion { Version = version });
                }

                foreach (TitleVersion version in title.Versions.Values.Where(x => x.Exists))
                {
                    ulong titleId = title.Id;
                    try
                    {
                        Cnmt meta = net.GetCnmt(titleId, version.Version);
                        version.ContentMetadata = meta;
                        if (meta == null)
                        {
                            version.Exists = false;
                            logger?.LogMessage($"{titleId:x16}v{version.Version} not found.");
                            continue;
                        }

                        Nacp control = net.GetControl(titleId, version.Version);
                        version.Control = control;

                        if (!net.Db.Titles.ContainsKey(meta.ApplicationTitleId))
                        {
                            net.Db.AddTitle(meta.ApplicationTitleId);
                            logger?.LogMessage($"Found title {meta.ApplicationTitleId:x16}");
                        }

                        if (meta.Type == TitleType.Application)
                        {
                            ReadSuperfly(title, net, logger);
                        }

                        logger?.LogMessage($"{titleId:x16}v{version.Version}");
                        //Thread.Sleep(300);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogMessage($"Failed getting {titleId:x16}v{version.Version}\n{ex.Message}");
                    }
                }
                // net.Save();
            }
        }

        private static void ReadSuperfly(TitleMetadata titleDb, NetContext net, IProgressReport logger = null)
        {
            titleDb.Superfly = net.GetSuperfly(titleDb.Id);

            foreach (SuperflyInfo title in titleDb.Superfly)
            {
                ulong id = ulong.Parse(title.title_id, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                net.Db.AddTitle(id, title.version);
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
        }

        private static List<ulong> GetTitleIds(string filename)
        {
            var titles = new List<ulong>();

            using (var reader = new StreamReader(new FileStream(filename, FileMode.Open, FileAccess.Read)))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (ulong.TryParse(line, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var id))
                    {
                        titles.Add(id);
                    }
                }
            }

            return titles;
        }
    }
}
