using System;
using System.IO;
using System.Text;
using LibHac;
using LibHac.Fs;

namespace hactoolnet
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                if (Run(args)) return 0;
            }
            catch (MissingKeyException ex)
            {
                string name = ex.Type == KeyType.Title ? $"Title key for rights ID {ex.Name}" : ex.Name;
                Console.Error.WriteLine($"\nERROR: {ex.Message}\nA required key is missing.\nKey name: {name}\n");
            }
            catch (HorizonResultException ex)
            {
                Console.Error.WriteLine($"\nERROR: {ex.Message}");

                if (ex.ResultValue != ex.InternalResultValue)
                {
                    Console.Error.WriteLine($"Internal Code: {ex.InternalResultValue.ToStringWithName()}");
                }

                Console.Error.WriteLine();
                Console.Error.WriteLine("Additional information:");
                Console.Error.WriteLine(ex.StackTrace);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"\nERROR: {ex.Message}\n");

                Console.Error.WriteLine("Additional information:");
                Console.Error.WriteLine(ex.GetType().FullName);
                Console.Error.WriteLine(ex.StackTrace);
            }

            return 1;
        }

        private static bool Run(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            var ctx = new Context();
            ctx.Options = CliParser.Parse(args);
            if (ctx.Options == null) return false;

            StreamWriter logWriter = null;
            ResultLogger resultLogger = null;

            try
            {
                using (var logger = new ProgressBar())
                {
                    ctx.Logger = logger;
                    ctx.Horizon = new Horizon(new StopWatchTimeSpanGenerator());

                    if (ctx.Options.AccessLog != null)
                    {
                        logWriter = new StreamWriter(ctx.Options.AccessLog);
                        var accessLog = new TextWriterAccessLog(logWriter);

                        ctx.Horizon.Fs.SetAccessLogTarget(AccessLogTarget.All);
                        ctx.Horizon.Fs.SetGlobalAccessLogMode(GlobalAccessLogMode.Log);

                        ctx.Horizon.Fs.SetAccessLogObject(accessLog);
                    }

                    if (ctx.Options.ResultLog != null)
                    {
                        resultLogger = new ResultLogger(new StreamWriter(ctx.Options.ResultLog),
                            printStackTrace: true, printSourceInfo: true, combineRepeats: true);

                        Result.SetLogger(resultLogger);
                    }

                    OpenKeyset(ctx);

                    if (ctx.Options.RunCustom)
                    {
                        CustomTask(ctx);
                        return true;
                    }

                    RunTask(ctx);
                }
            }
            finally
            {
                logWriter?.Dispose();

                if (resultLogger != null)
                {
                    Result.SetLogger(null);
                    resultLogger.Dispose();
                }
            }

            return true;
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
                    ProcessNax0.Process(ctx);
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
            string keyFileName = ctx.Options.UseDevKeys ? "dev.keys" : "prod.keys";

            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string homeKeyFile = Path.Combine(home, ".switch", keyFileName);
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

            ctx.Keyset = ExternalKeyReader.ReadKeyFile(keyFile, titleKeyFile, consoleKeyFile, ctx.Logger, ctx.Options.UseDevKeys);
            if (ctx.Options.SdSeed != null)
            {
                ctx.Keyset.SetSdSeed(ctx.Options.SdSeed.ToBytes());
            }

            if (ctx.Options.InFileType == FileType.Keygen && ctx.Options.OutDir != null)
            {
                string dir = ctx.Options.OutDir;
                Directory.CreateDirectory(dir);

                File.WriteAllText(Path.Combine(dir, keyFileName), ExternalKeyReader.PrintCommonKeys(ctx.Keyset));
                File.WriteAllText(Path.Combine(dir, "console.keys"), ExternalKeyReader.PrintUniqueKeys(ctx.Keyset));
                File.WriteAllText(Path.Combine(dir, "title.keys"), ExternalKeyReader.PrintTitleKeys(ctx.Keyset));
            }
        }

        private static void ProcessKeygen(Context ctx)
        {
            Console.WriteLine(ExternalKeyReader.PrintCommonKeys(ctx.Keyset));
        }

        // For running random stuff
        // ReSharper disable once UnusedParameter.Local
        private static void CustomTask(Context ctx)
        {

        }
    }
}
