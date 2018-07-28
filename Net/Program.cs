using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using libhac;

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

            var tid = ctx.Options.TitleId;
            var ver = ctx.Options.Version;

            var net = new NetContext(ctx);
            //GetControls(net);

            var cnmt = net.GetCnmt(tid, ver);
            foreach (var entry in cnmt.ContentEntries)
            {
                Console.WriteLine($"{entry.NcaId.ToHexString()} {entry.Type}");
                net.GetNcaFile(tid, ver, entry.NcaId.ToHexString());
            }

            var control = net.GetControl(tid, ver);
            ;
        }

        private static void GetControls(NetContext net)
        {
            var titles = GetTitleIds("titles.txt");

            foreach (var title in titles)
            {
                var control = net.GetControl(title, 0);
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

