using System.IO;
using System.Text.Json;
using LibHac.FsSystem;
using LibHac.Tools.FsSystem;
using LibHac.Tools.Npdm;

namespace hactoolnet;

internal static class ProcessNpdm
{
    public static void Process(Context ctx)
    {
        using (var file = new LocalStorage(ctx.Options.InFile, FileAccess.Read))
        {
            var npdm = new NpdmBinary(file.AsStream(), ctx.KeySet);

            if(ctx.Options.JsonFile != null)
            {
                string json = JsonSerializer.Serialize(npdm, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(ctx.Options.JsonFile, json);
            }
        }
    }
}