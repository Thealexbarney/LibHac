using System;
using System.IO;
using System.Text;
using LibHac;

namespace hactoolnet
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                Run(args);
            }
            catch (MissingKeyException ex)
            {
                string name = ex.Type == KeyType.Title ? $"Title key for rights ID {ex.Name}" : ex.Name;
                Console.WriteLine($"\nERROR: {ex.Message}\nA required key is missing.\nKey name: {name}\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nERROR: {ex.Message}\n");

                Console.WriteLine("Additional information:");
                Console.WriteLine(ex.GetType().FullName);
                Console.WriteLine(ex.StackTrace);
            }
        }

        private static void Run(string[] args)
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

                RunTask(ctx);
            }
        }

        private static void RunTask(Context ctx)
        {
            switch (ctx.Options.InFileType)
            {
                case FileType.Nca:
                    ProcessNca.Process(ctx);
                    break;
                case FileType.Pfs0:
                case FileType.Nsp:
                    ProcessPfs.Process(ctx);
                    break;
                case FileType.PfsBuild:
                    ProcessFsBuild.ProcessPartitionFs(ctx);
                    break;
                case FileType.Romfs:
                    ProcessRomfs.Process(ctx);
                    break;
                case FileType.RomfsBuild:
                    ProcessFsBuild.ProcessRomFs(ctx);
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
                case FileType.Ndv0:
                    ProcessDelta.Process(ctx);
                    break;
                case FileType.Bench:
                    ProcessBench.Process(ctx);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static void OpenKeyset(Context ctx)
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string homeKeyFile = Path.Combine(home, ".switch", "prod.keys");
            string homeTitleKeyFile = Path.Combine(home, ".switch", "title.keys");
            string homeConsoleKeyFile = Path.Combine(home, ".switch", "console.keys");
            string keyFile = ctx.Options.Keyfile;
            string titleKeyFile = ctx.Options.TitleKeyFile;
            string consoleKeyFile = ctx.Options.ConsoleKeyFile;

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

            if (ctx.Options.InFileType == FileType.Keygen && ctx.Options.OutDir != null)
            {
                string dir = ctx.Options.OutDir;
                Directory.CreateDirectory(dir);

                File.WriteAllText(Path.Combine(dir, "prod.keys"), ExternalKeys.PrintCommonKeys(ctx.Keyset));
                File.WriteAllText(Path.Combine(dir, "console.keys"), ExternalKeys.PrintUniqueKeys(ctx.Keyset));
                File.WriteAllText(Path.Combine(dir, "title.keys"), ExternalKeys.PrintTitleKeys(ctx.Keyset));
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
