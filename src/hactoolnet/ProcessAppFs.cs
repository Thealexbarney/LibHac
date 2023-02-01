using System.Linq;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Ncm;
using LibHac.Spl;
using LibHac.Tools.Es;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;

namespace hactoolnet;

internal static class ProcessAppFs
{
    private static void ImportTickets(Context ctx, IFileSystem fileSystem)
    {
        foreach (DirectoryEntryEx entry in fileSystem.EnumerateEntries("*.tik", SearchOptions.Default))
        {
            using var tikFile = new UniqueRef<IFile>();
            fileSystem.OpenFile(ref tikFile.Ref, entry.FullPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();

            var ticket = new Ticket(tikFile.Get.AsStream());

            if (ticket.RightsId.IsZeros())
                continue;

            byte[] key = ticket.GetTitleKey(ctx.KeySet);
            if (key is null)
                continue;

            var rightsId = SpanHelpers.AsStruct<RightsId>(ticket.RightsId);
            var accessKey = SpanHelpers.AsStruct<AccessKey>(key);

            ctx.KeySet.ExternalKeySet.Add(rightsId, accessKey).ThrowIfFailure();
        }
    }

    public static void Process(Context ctx, IFileSystem fileSystem)
    {
        ImportTickets(ctx, fileSystem);

        SwitchFs switchFs = SwitchFs.OpenNcaDirectory(ctx.KeySet, fileSystem);

        if (ctx.Options.ListNcas)
        {
            ctx.Logger.LogMessage(ProcessSwitchFs.ListNcas(switchFs));
        }

        if (ctx.Options.ListTitles)
        {
            ctx.Logger.LogMessage(ProcessSwitchFs.ListTitles(switchFs));
        }

        if (ctx.Options.ListApps)
        {
            ctx.Logger.LogMessage(ProcessSwitchFs.ListApplications(switchFs));
        }

        ulong id = GetTargetProgramId(ctx, switchFs);
        if (id == ulong.MaxValue)
        {
            ctx.Logger.LogMessage("Title ID must be specified to dump ExeFS or RomFS");
            return;
        }

        if (!switchFs.Titles.TryGetValue(id, out Title title))
        {
            ctx.Logger.LogMessage($"Could not find title {id:X16}");
            return;
        }

        if (title.MainNca == null)
        {
            ctx.Logger.LogMessage($"Could not find main data for title {id:X16}");
            return;
        }

        if (title.Metadata?.ContentMetaAttributes.HasFlag(ContentMetaAttribute.Compacted) == true)
        {
            ctx.Logger.LogMessage($"Cannot extract compacted NCAs");
            return;
        }

        if (ctx.Options.ExefsOutDir != null || ctx.Options.ExefsOut != null)
        {
            if (!title.MainNca.Nca.SectionExists(NcaSectionType.Code))
            {
                ctx.Logger.LogMessage($"Main NCA for title {id:X16} has no ExeFS section");
                return;
            }

            if (ctx.Options.ExefsOutDir != null)
            {
                IFileSystem fs = title.MainNca.OpenFileSystem(NcaSectionType.Code, ctx.Options.IntegrityLevel);
                fs.Extract(ctx.Options.ExefsOutDir, ctx.Logger);
            }

            if (ctx.Options.ExefsOut != null)
            {
                title.MainNca.Nca.ExportSection(NcaSectionType.Code, ctx.Options.ExefsOut, ctx.Options.Raw, ctx.Options.IntegrityLevel, ctx.Logger);
            }
        }

        if (ctx.Options.RomfsOutDir != null || ctx.Options.RomfsOut != null || ctx.Options.ListRomFs)
        {
            if (!title.MainNca.Nca.SectionExists(NcaSectionType.Data))
            {
                ctx.Logger.LogMessage($"Main NCA for title {id:X16} has no RomFS section");
                return;
            }

            ProcessRomfs.Process(ctx, title.MainNca.OpenStorage(NcaSectionType.Data, ctx.Options.IntegrityLevel));
        }

        if (ctx.Options.NspOut != null)
        {
            ProcessPfs.CreateNsp(ctx, switchFs);
        }
    }

    private static ulong GetTargetProgramId(Context ctx, SwitchFs switchFs)
    {
        ulong id = ctx.Options.TitleId;
        if (id != 0)
        {
            return id;
        }

        if (switchFs.Applications.Count != 1)
        {
            return ulong.MaxValue;
        }

        ulong applicationId = switchFs.Applications.Values.First().TitleId;
        ulong updateId = applicationId | 0x800ul;

        if (switchFs.Titles.ContainsKey(updateId))
        {
            return updateId;
        }

        return applicationId;
    }
}