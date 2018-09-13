using System.IO;
using System.Linq;
using LibHac;

namespace hactoolnet
{
    internal static class ProcessNca
    {
        public static void Process(Context ctx)
        {
            using (var file = new FileStream(ctx.Options.InFile, FileMode.Open, FileAccess.Read))
            {
                var nca = new Nca(ctx.Keyset, file, false);

                if (ctx.Options.BaseNca != null)
                {
                    var baseFile = new FileStream(ctx.Options.BaseNca, FileMode.Open, FileAccess.Read);
                    var baseNca = new Nca(ctx.Keyset, baseFile, false);
                    nca.SetBaseNca(baseNca);
                }

                for (int i = 0; i < 3; i++)
                {
                    if (ctx.Options.SectionOut[i] != null)
                    {
                        nca.ExportSection(i, ctx.Options.SectionOut[i], ctx.Options.Raw, ctx.Logger);
                    }

                    if (ctx.Options.SectionOutDir[i] != null)
                    {
                        nca.ExtractSection(i, ctx.Options.SectionOutDir[i], ctx.Logger);
                    }

                    if (ctx.Options.Validate && nca.Sections[i] != null)
                    {
                        nca.VerifySection(i, ctx.Logger);
                    }
                }

                if (ctx.Options.ListRomFs && nca.Sections[1] != null)
                {
                    var romfs = new Romfs(nca.OpenSection(1, false));

                    foreach (var romfsFile in romfs.Files)
                    {
                        ctx.Logger.LogMessage(romfsFile.FullPath);
                    }
                }

                if (ctx.Options.RomfsOutDir != null || ctx.Options.RomfsOut != null)
                {
                    NcaSection section = nca.Sections.FirstOrDefault(x => x?.Type == SectionType.Romfs || x?.Type == SectionType.Bktr);

                    if (section == null)
                    {
                        ctx.Logger.LogMessage("NCA has no RomFS section");
                        return;
                    }

                    if (section.Type == SectionType.Bktr && ctx.Options.BaseNca == null)
                    {
                        ctx.Logger.LogMessage("Cannot save BKTR section without base RomFS");
                        return;
                    }

                    if (ctx.Options.RomfsOut != null)
                    {
                        nca.ExportSection(section.SectionNum, ctx.Options.RomfsOut, ctx.Options.Raw, ctx.Logger);
                    }

                    if (ctx.Options.RomfsOutDir != null)
                    {
                        var romfs = new Romfs(nca.OpenSection(section.SectionNum, false));
                        romfs.Extract(ctx.Options.RomfsOutDir, ctx.Logger);
                    }
                }

                if (ctx.Options.ExefsOutDir != null || ctx.Options.ExefsOut != null)
                {
                    NcaSection section = nca.Sections.FirstOrDefault(x => x.IsExefs);

                    if (section == null)
                    {
                        ctx.Logger.LogMessage("Could not find an ExeFS section");
                        return;
                    }

                    if (ctx.Options.ExefsOut != null)
                    {
                        nca.ExportSection(section.SectionNum, ctx.Options.ExefsOut, ctx.Options.Raw, ctx.Logger);
                    }

                    if (ctx.Options.ExefsOutDir != null)
                    {
                        nca.ExtractSection(section.SectionNum, ctx.Options.ExefsOutDir, ctx.Logger);
                    }
                }

                ctx.Logger.LogMessage(nca.Dump());
            }
        }
    }
}
