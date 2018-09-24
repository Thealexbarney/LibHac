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
                    case FileType.Pfs0:
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
                        ProcessSave(ctx);
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

        private static void ProcessSave(Context ctx)
        {
            using (var file = new FileStream(ctx.Options.InFile, FileMode.Open, FileAccess.ReadWrite))
            {
                var save = new Savefile(ctx.Keyset, file, ctx.Options.EnableHash, ctx.Logger);

                if (ctx.Options.OutDir != null)
                {
                    save.Extract(ctx.Options.OutDir, ctx.Logger);
                }

                if (ctx.Options.DebugOutDir != null)
                {
                    var dir = ctx.Options.DebugOutDir;
                    Directory.CreateDirectory(dir);

                    File.WriteAllBytes(Path.Combine(dir, "L0_0_MasterHashA"), save.Header.MasterHashA);
                    File.WriteAllBytes(Path.Combine(dir, "L0_1_MasterHashB"), save.Header.MasterHashB);
                    File.WriteAllBytes(Path.Combine(dir, "L0_2_DuplexMasterA"), save.Header.DuplexMasterA);
                    File.WriteAllBytes(Path.Combine(dir, "L0_3_DuplexMasterB"), save.Header.DuplexMasterB);
                    save.DuplexL1A.WriteAllBytes(Path.Combine(dir, "L0_4_DuplexL1A"), ctx.Logger);
                    save.DuplexL1B.WriteAllBytes(Path.Combine(dir, "L0_5_DuplexL1B"), ctx.Logger);
                    save.DuplexDataA.WriteAllBytes(Path.Combine(dir, "L0_6_DuplexDataA"), ctx.Logger);
                    save.DuplexDataB.WriteAllBytes(Path.Combine(dir, "L0_7_DuplexDataB"), ctx.Logger);
                    save.JournalData.WriteAllBytes(Path.Combine(dir, "L0_9_JournalData"), ctx.Logger);

                    save.DuplexData.WriteAllBytes(Path.Combine(dir, "L1_0_DuplexData"), ctx.Logger);
                    save.JournalTable.WriteAllBytes(Path.Combine(dir, "L2_0_JournalTable"), ctx.Logger);
                    save.JournalBitmapUpdatedPhysical.WriteAllBytes(Path.Combine(dir, "L2_1_JournalBitmapUpdatedPhysical"), ctx.Logger);
                    save.JournalBitmapUpdatedVirtual.WriteAllBytes(Path.Combine(dir, "L2_2_JournalBitmapUpdatedVirtual"), ctx.Logger);
                    save.JournalBitmapUnassigned.WriteAllBytes(Path.Combine(dir, "L2_3_JournalBitmapUnassigned"), ctx.Logger);
                    save.JournalLayer1Hash.WriteAllBytes(Path.Combine(dir, "L2_4_Layer1Hash"), ctx.Logger);
                    save.JournalLayer2Hash.WriteAllBytes(Path.Combine(dir, "L2_5_Layer2Hash"), ctx.Logger);
                    save.JournalLayer3Hash.WriteAllBytes(Path.Combine(dir, "L2_6_Layer3Hash"), ctx.Logger);
                    save.JournalFat.WriteAllBytes(Path.Combine(dir, "L2_7_FAT"), ctx.Logger);

                    save.IvfcStreamSource.CreateStream().WriteAllBytes(Path.Combine(dir, "L3_0_SaveData"), ctx.Logger);
                }

                if (ctx.Options.SignSave)
                {
                    if (save.SignHeader(ctx.Keyset))
                    {
                        ctx.Logger.LogMessage("Successfully signed save file");
                    }
                    else
                    {
                        ctx.Logger.LogMessage("Unable to sign save file. Do you have all the required keys?");
                    }
                }
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
