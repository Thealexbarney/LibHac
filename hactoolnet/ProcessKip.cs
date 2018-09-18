using System.IO;
using LibHac;

namespace hactoolnet
{
    internal static class ProcessKip
    {
        public static void ProcessKip1(Context ctx)
        {
            using (var file = new FileStream(ctx.Options.InFile, FileMode.Open, FileAccess.Read))
            {
                var kip = new Kip(file);
            }
        }
    }
}
