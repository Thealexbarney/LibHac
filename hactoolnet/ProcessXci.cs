using System.IO;
using System.Linq;
using LibHac;

namespace hactoolnet
{
    internal static class ProcessXci
    {
        public static void Process(Context ctx)
        {
            using (var file = new FileStream(ctx.Options.InFile, FileMode.Open, FileAccess.Read))
            {
                var xci = new Xci(ctx.Keyset, file);

                if (ctx.Options.RootDir != null)
                {
                    xci.RootPartition?.Extract(ctx.Options.RootDir, ctx.Logger);
                }

                if (ctx.Options.UpdateDir != null)
                {
                    xci.UpdatePartition?.Extract(ctx.Options.UpdateDir, ctx.Logger);
                }

                if (ctx.Options.NormalDir != null)
                {
                    xci.NormalPartition?.Extract(ctx.Options.NormalDir, ctx.Logger);
                }

                if (ctx.Options.SecureDir != null)
                {
                    xci.SecurePartition?.Extract(ctx.Options.SecureDir, ctx.Logger);
                }

                if (ctx.Options.LogoDir != null)
                {
                    xci.LogoPartition?.Extract(ctx.Options.LogoDir, ctx.Logger);
                }

                if (ctx.Options.OutDir != null && xci.RootPartition != null)
                {
                    var root = xci.RootPartition;
                    if (root == null)
                    {
                        ctx.Logger.LogMessage("Could not find root partition");
                        return;
                    }

                    foreach (var sub in root.Files)
                    {
                        var subPfs = new Pfs(root.OpenFile(sub));
                        var subDir = Path.Combine(ctx.Options.OutDir, sub.Name);

                        subPfs.Extract(subDir, ctx.Logger);
                    }
                }

                if (ctx.Options.ExefsOutDir != null || ctx.Options.ExefsOut != null)
                {
                    var mainNca = GetXciMainNca(xci, ctx);

                    if (mainNca == null)
                    {
                        ctx.Logger.LogMessage("Could not find Program NCA");
                        return;
                    }

                    var exefsSection = mainNca.Sections.FirstOrDefault(x => x.IsExefs);

                    if (exefsSection == null)
                    {
                        ctx.Logger.LogMessage("NCA has no ExeFS section");
                        return;
                    }

                    if (ctx.Options.ExefsOutDir != null)
                    {
                        mainNca.ExtractSection(exefsSection.SectionNum, ctx.Options.ExefsOutDir, ctx.Logger);
                    }

                    if (ctx.Options.ExefsOut != null)
                    {
                        mainNca.ExportSection(exefsSection.SectionNum, ctx.Options.ExefsOut, ctx.Options.Raw, ctx.Logger);
                    }
                }

                if (ctx.Options.RomfsOutDir != null || ctx.Options.RomfsOut != null)
                {
                    var mainNca = GetXciMainNca(xci, ctx);

                    if (mainNca == null)
                    {
                        ctx.Logger.LogMessage("Could not find Program NCA");
                        return;
                    }

                    var romfsSection = mainNca.Sections.FirstOrDefault(x => x.Type == SectionType.Romfs);

                    if (romfsSection == null)
                    {
                        ctx.Logger.LogMessage("NCA has no RomFS section");
                        return;
                    }

                    if (ctx.Options.RomfsOutDir != null)
                    {
                        var romfs = new Romfs(mainNca.OpenSection(romfsSection.SectionNum, false));
                        romfs.Extract(ctx.Options.RomfsOutDir, ctx.Logger);
                    }

                    if (ctx.Options.RomfsOut != null)
                    {
                        mainNca.ExportSection(romfsSection.SectionNum, ctx.Options.RomfsOut, ctx.Options.Raw, ctx.Logger);
                    }
                }
            }
        }

        private static Nca GetXciMainNca(Xci xci, Context ctx)
        {
            if (xci.SecurePartition == null)
            {
                ctx.Logger.LogMessage("Could not find secure partition");
                return null;
            }

            Nca mainNca = null;

            foreach (var fileEntry in xci.SecurePartition.Files.Where(x => x.Name.EndsWith(".nca")))
            {
                var ncaStream = xci.SecurePartition.OpenFile(fileEntry);
                var nca = new Nca(ctx.Keyset, ncaStream, true);

                if (nca.Header.ContentType == ContentType.Program)
                {
                    mainNca = nca;
                }
            }

            return mainNca;
        }
    }
}
