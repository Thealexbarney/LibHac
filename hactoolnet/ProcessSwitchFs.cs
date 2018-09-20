using System;
using System.IO;
using System.Linq;
using System.Text;
using LibHac;
using LibHac.Savefile;

namespace hactoolnet
{
    internal static class ProcessSwitchFs
    {
        public static void Process(Context ctx)
        {
            var switchFs = new SwitchFs(ctx.Keyset, new FileSystem(ctx.Options.InFile));

            if (ctx.Options.ListTitles)
            {
                ListTitles(switchFs);
            }

            if (ctx.Options.ListApps)
            {
                ctx.Logger.LogMessage(ListApplications(switchFs));
            }

            if (ctx.Options.ExefsOutDir != null || ctx.Options.ExefsOut != null)
            {
                var id = ctx.Options.TitleId;
                if (id == 0)
                {
                    ctx.Logger.LogMessage("Title ID must be specified to dump ExeFS");
                    return;
                }

                if (!switchFs.Titles.TryGetValue(id, out var title))
                {
                    ctx.Logger.LogMessage($"Could not find title {id:X16}");
                    return;
                }

                if (title.MainNca == null)
                {
                    ctx.Logger.LogMessage($"Could not find main data for title {id:X16}");
                    return;
                }

                var section = title.MainNca.Sections.FirstOrDefault(x => x.IsExefs);

                if (section == null)
                {
                    ctx.Logger.LogMessage($"Main NCA for title {id:X16} has no ExeFS section");
                    return;
                }

                if (ctx.Options.ExefsOutDir != null)
                {
                    title.MainNca.ExtractSection(section.SectionNum, ctx.Options.ExefsOutDir, ctx.Options.EnableHash, ctx.Logger);
                }

                if (ctx.Options.ExefsOut != null)
                {
                    title.MainNca.ExportSection(section.SectionNum, ctx.Options.ExefsOut, ctx.Options.Raw, ctx.Options.EnableHash, ctx.Logger);
                }
            }

            if (ctx.Options.RomfsOutDir != null || ctx.Options.RomfsOut != null)
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

                if (title.MainNca == null)
                {
                    ctx.Logger.LogMessage($"Could not find main data for title {id:X16}");
                    return;
                }

                var section = title.MainNca.Sections.FirstOrDefault(x => x?.Type == SectionType.Romfs || x?.Type == SectionType.Bktr);

                if (section == null)
                {
                    ctx.Logger.LogMessage($"Main NCA for title {id:X16} has no RomFS section");
                    return;
                }

                if (ctx.Options.RomfsOutDir != null)
                {
                    var romfs = new Romfs(title.MainNca.OpenSection(section.SectionNum, false, ctx.Options.EnableHash));
                    romfs.Extract(ctx.Options.RomfsOutDir, ctx.Logger);
                }

                if (ctx.Options.RomfsOut != null)
                {
                    title.MainNca.ExportSection(section.SectionNum, ctx.Options.RomfsOut, ctx.Options.Raw, ctx.Options.EnableHash, ctx.Logger);
                }
            }

            if (ctx.Options.OutDir != null)
            {
                SaveTitle(ctx, switchFs);
            }

            if (ctx.Options.NspOut != null)
            {
                ProcessNsp.CreateNsp(ctx, switchFs);
            }

            if (ctx.Options.SaveOutDir != null)
            {
                ExportSdSaves(ctx, switchFs);
            }
        }

        private static void SaveTitle(Context ctx, SwitchFs switchFs)
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
                var stream = nca.GetStream();
                var outFile = Path.Combine(saveDir, nca.Filename);
                ctx.Logger.LogMessage(nca.Filename);
                using (var outStream = new FileStream(outFile, FileMode.Create, FileAccess.ReadWrite))
                {
                    stream.CopyStream(outStream, stream.Length, ctx.Logger);
                }
            }
        }

        static void ListTitles(SwitchFs sdfs)
        {
            foreach (var title in sdfs.Titles.Values.OrderBy(x => x.Id))
            {
                Console.WriteLine($"{title.Name} {title.Control?.DisplayVersion}");
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

        static string ListApplications(SwitchFs sdfs)
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
                if (app.Nacp?.BcatDeliveryCacheStorageSize > 0)
                    sb.AppendLine($"BCAT save: {Util.GetBytesReadable(app.Nacp.BcatDeliveryCacheStorageSize)}");

                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static void ExportSdSaves(Context ctx, SwitchFs switchFs)
        {
            foreach (var save in switchFs.Saves)
            {
                var outDir = Path.Combine(ctx.Options.SaveOutDir, save.Key);
                save.Value.Extract(outDir, ctx.Logger);
            }
        }
    }
}
