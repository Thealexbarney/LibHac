using System;
using System.IO;
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
                        ProcessNca.Process(ctx);
                        break;
                    case FileType.Pfs0: case FileType.Nsp:
                        ProcessNsp.Process(ctx);
                        break;
                    case FileType.Romfs:
                        ProcessRomfs.Process(ctx);
                        break;
                    case FileType.Nax0:
                        break;
                    case FileType.SwitchFs:
                        ProcessSwitchFs.Process(ctx);
                        break;
                    case FileType.Save:
                        ProcessSave.Process(ctx);
                        break;
                    case FileType.Xci:
                        ProcessXci.Process(ctx);
                        break;
                    case FileType.Keygen:
                        ProcessKeygen(ctx);
                        break;
                    case FileType.Pk11:
                        ProcessPackage.ProcessPk11(ctx);
                        break;
                    case FileType.Pk21:
                        ProcessPackage.ProcessPk21(ctx);
                        break;
                    case FileType.Kip1:
                        ProcessKip.ProcessKip1(ctx);
                        break;
                    case FileType.Ini1:
                        ProcessKip.ProcessIni1(ctx);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
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

        private static void ProcessKeygen(Context ctx)
        {
            Console.WriteLine(ExternalKeys.PrintCommonKeys(ctx.Keyset));
        }

        // For running random stuff
        // ReSharper disable once UnusedParameter.Local
        private static void CustomTask(Context ctx)
        {        
            
        }
    }
}
