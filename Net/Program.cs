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

            var tid = ctx.Options.TitleId;
            var ver = ctx.Options.Version;

            var net = new NetContext(ctx);
            var cnmt = net.GetCnmt(tid, ver);
            if (cnmt == null) return;
            ctx.Logger.LogMessage($"Title is of type {cnmt.Type} and has {cnmt.ContentEntries.Length} content entries");
            var control = net.GetControl(tid, ver);
            if (control != null)
            {
                ctx.Logger.LogMessage($"Title has name {control.Languages[0].Title}");
            }
            foreach (var entry in cnmt.ContentEntries)
            {
                ctx.Logger.LogMessage($"{entry.NcaId.ToHexString()} {entry.Type}");
                net.GetNcaFile(tid, ver, entry.NcaId.ToHexString());
            }
        }

        private static void GetMetadata(NetContext net, IProgressReport logger = null)
        {
            var versionList = net.GetVersionList();
            net.Db.ImportVersionList(versionList);
            //net.Db.ImportList("titles.txt");
            net.Save();

            foreach (var title in net.Db.Titles.Values)
            {
                foreach (var version in title.Versions.Values.Where(x => x.Exists))
                {
                    var titleId = version.Version == 0 ? title.Id : title.UpdateId;
                    try
                    {
                        var control = net.GetControl((ulong)titleId, version.Version);
                        version.Control = control;
                        if (control == null) version.Exists = false;

                        Cnmt meta = net.GetCnmt((ulong)titleId, version.Version);
                        version.ContentMetadata = meta;
                        if (meta == null) version.Exists = false;

                        logger?.LogMessage($"{titleId}v{version.Version}");
                        //Thread.Sleep(300);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogMessage($"Failed getting {titleId}v{version.Version}\n{ex.Message}");
                    }
                }
                // net.Save();
            }

            net.Save();
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
