using System;
using System.IO;
using System.Text;
using LibHac;
using LibHac.Common.Keys;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Util;

namespace hactoolnet
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                if (Run(args))
                {
                    return 0;
                }
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
#if !CORERT_NO_REFLECTION
                Console.Error.WriteLine(ex.GetType().FullName);
#endif

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
            LogObserverHolder logObserver = null;

            try
            {
                using (var logger = new ProgressBar())
                {
                    ctx.Logger = logger;
                    OpenKeySet(ctx);
                    InitializeHorizon(ctx);

                    if (ctx.Options.AccessLog != null)
                    {
                        logWriter = new StreamWriter(ctx.Options.AccessLog);
                        logObserver = new LogObserverHolder();

                        // ReSharper disable once AccessToDisposedClosure
                        // References to logWriter should be gone by the time it's disposed
                        ctx.Horizon.Diag.InitializeLogObserverHolder(ref logObserver,
                            (in LogMetaData data, in LogBody body, object arguments) =>
                                logWriter.Write(body.Message.ToString()), null);

                        ctx.Horizon.Diag.RegisterLogObserver(logObserver);

                        ctx.Horizon.Fs.SetLocalSystemAccessLogForDebug(true);
                        ctx.Horizon.Fs.SetGlobalAccessLogMode(GlobalAccessLogMode.Log).ThrowIfFailure();
                    }

                    if (ctx.Options.ResultLog != null)
                    {
                        resultLogger = new ResultLogger(new StreamWriter(ctx.Options.ResultLog),
                            printStackTrace: true, printSourceInfo: true, combineRepeats: true);

                        Result.SetLogger(resultLogger);
                    }

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
                if (logObserver != null)
                {
                    ctx.Horizon.Diag.UnregisterLogObserver(logObserver);
                }

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

        private static void OpenKeySet(Context ctx)
        {
#if CORERT_NO_REFLECTION
            string home = HomeFolder.GetFolderPath(Environment.SpecialFolder.UserProfile);
#else
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
#endif
            string homeTitleKeyFile = Path.Combine(home, ".switch", "title.keys");
            string homeConsoleKeyFile = Path.Combine(home, ".switch", "console.keys");

            string prodKeyFile = Path.Combine(home, ".switch", "prod.keys");
            string devKeyFile = Path.Combine(home, ".switch", "dev.keys");
            string titleKeyFile = ctx.Options.TitleKeyFile;
            string consoleKeyFile = ctx.Options.ConsoleKeyFile;

            // Check if the files from the command line exist
            if (titleKeyFile != null && !File.Exists(titleKeyFile))
                titleKeyFile = null;

            if (consoleKeyFile != null && !File.Exists(consoleKeyFile))
                consoleKeyFile = null;

            if (!File.Exists(prodKeyFile))
                prodKeyFile = null;

            if (!File.Exists(devKeyFile))
                devKeyFile = null;

            // Check the home directory if no existing key files were specified
            if (consoleKeyFile == null && File.Exists(homeConsoleKeyFile))
                consoleKeyFile = homeConsoleKeyFile;

            if (titleKeyFile == null && File.Exists(homeTitleKeyFile))
                titleKeyFile = homeTitleKeyFile;

            var keySet = KeySet.CreateDefaultKeySet();

            // If the user specifies a key file then only load that file into the mode they specified,
            // otherwise load both prod.keys and dev.keys.
            // Todo: Should we add a way that both dev-only key files and mixed prod/dev key files
            // can both be loaded when specifying a key file in dev mode?
            if (ctx.Options.Keyfile != null && File.Exists(ctx.Options.Keyfile))
            {
                keySet.SetMode(ctx.Options.KeyMode);
                ExternalKeyReader.ReadKeyFile(keySet, ctx.Options.Keyfile, titleKeyFile, consoleKeyFile, ctx.Logger);
            }
            else
            {
                ExternalKeyReader.ReadKeyFile(keySet, prodKeyFile, devKeyFile, titleKeyFile, consoleKeyFile, ctx.Logger);
            }

            keySet.SetMode(ctx.Options.KeyMode);

            if (ctx.Options.SdSeed != null)
            {
                keySet.SetSdSeed(ctx.Options.SdSeed.ToBytes());
            }

            ctx.KeySet = keySet;
        }

        private static void InitializeHorizon(Context ctx)
        {
            var horizon = new Horizon(new HorizonConfiguration());
            ctx.Horizon = horizon.CreatePrivilegedHorizonClient();
            ctx.Horizon.Fs.SetServerlessAccessLog(true);
        }

        private static void ProcessKeygen(Context ctx)
        {
            Console.WriteLine(ExternalKeyWriter.PrintAllKeys(ctx.KeySet));

            if (ctx.Options.OutDir != null)
            {
                KeySet.Mode originalMode = ctx.KeySet.CurrentMode;

                string dir = ctx.Options.OutDir;
                Directory.CreateDirectory(dir);

                ctx.KeySet.SetMode(KeySet.Mode.Prod);
                File.WriteAllText(Path.Combine(dir, "prod.keys"), ExternalKeyWriter.PrintCommonKeys(ctx.KeySet));

                ctx.KeySet.SetMode(KeySet.Mode.Dev);
                File.WriteAllText(Path.Combine(dir, "dev.keys"), ExternalKeyWriter.PrintCommonKeys(ctx.KeySet));

                ctx.KeySet.SetMode(originalMode);
                File.WriteAllText(Path.Combine(dir, "console.keys"), ExternalKeyWriter.PrintDeviceKeys(ctx.KeySet));
                File.WriteAllText(Path.Combine(dir, "title.keys"), ExternalKeyWriter.PrintTitleKeys(ctx.KeySet));

                File.WriteAllText(Path.Combine(dir, "prod+dev.keys"),
                    ExternalKeyWriter.PrintCommonKeysWithDev(ctx.KeySet));
            }
        }

        // For running random stuff
        // ReSharper disable once UnusedParameter.Local
        private static void CustomTask(Context ctx)
        {

        }
    }
}
