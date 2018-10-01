using System;
using System.IO;
using System.Linq;
using System.Text;
using LibHac;
using LibHac.Savefile;
using static hactoolnet.Print;

namespace hactoolnet
{
    internal static class ProcessSave
    {
        public static void Process(Context ctx)
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

                ctx.Logger.LogMessage(save.Print());
            }
        }

        private static string Print(this Savefile save)
        {
            int colLen = 25;
            var sb = new StringBuilder();
            sb.AppendLine();

            sb.AppendLine("Savefile:");
            PrintItem(sb, colLen, "CMAC Signature:", save.Header.Cmac);
            PrintItem(sb, colLen, "Title ID:", $"{save.Header.ExtraData.TitleId:x16}");
            PrintItem(sb, colLen, "User ID:", save.Header.ExtraData.UserId);
            PrintItem(sb, colLen, "Save ID:", $"{save.Header.ExtraData.SaveId:x16}");
            PrintItem(sb, colLen, "Save Type:", $"{save.Header.ExtraData.Type}");
            PrintItem(sb, colLen, "Owner ID:", $"{save.Header.ExtraData.SaveOwnerId:x16}");
            PrintItem(sb, colLen, "Timestamp:", $"{DateTimeOffset.FromUnixTimeSeconds(save.Header.ExtraData.Timestamp):yyyy-MM-dd HH:mm:ss} UTC");
            PrintItem(sb, colLen, "Save Data Size:", $"0x{save.Header.ExtraData.DataSize:x16} ({Util.GetBytesReadable(save.Header.ExtraData.DataSize)})");
            PrintItem(sb, colLen, "Journal Size:", $"0x{save.Header.ExtraData.JournalSize:x16} ({Util.GetBytesReadable(save.Header.ExtraData.JournalSize)})");
            PrintItem(sb, colLen, $"Header Hash{save.Header.HeaderHashValidity.GetValidityString()}:", save.Header.Layout.Hash);
            PrintItem(sb, colLen, "IVFC Salt Seed:", save.Header.Ivfc.SaltSource);
            PrintItem(sb, colLen, "Number of Files:", save.Files.Length);

            if (save.Files.Length > 0 && save.Files.Length < 100)
            {
                sb.AppendLine("Files:");
                foreach (FileEntry file in save.Files.OrderBy(x => x.FullPath))
                {
                    sb.AppendLine(file.FullPath);
                }
            }

            return sb.ToString();
        }
    }
}
