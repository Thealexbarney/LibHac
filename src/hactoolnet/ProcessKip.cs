using System.IO;
using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Kernel;
using LibHac.Tools.FsSystem;

namespace hactoolnet;

internal static class ProcessKip
{
    public static void ProcessKip1(Context ctx)
    {
        using var file = new SharedRef<IStorage>(new LocalStorage(ctx.Options.InFile, FileAccess.Read));

        using var kip = new KipReader();
        kip.Initialize(in file).ThrowIfFailure();

        if (!string.IsNullOrWhiteSpace(ctx.Options.UncompressedOut))
        {
            byte[] uncompressed = new byte[kip.GetUncompressedSize()];

            kip.ReadUncompressedKip(uncompressed).ThrowIfFailure();

            File.WriteAllBytes(ctx.Options.UncompressedOut, uncompressed);
        }
    }

    public static void ProcessIni1(Context ctx)
    {
        using var file = new SharedRef<IStorage>(new LocalStorage(ctx.Options.InFile, FileAccess.Read));

        string outDir = ctx.Options.OutDir;

        if (outDir != null)
        {
            ExtractIni1(in file, outDir);
        }
    }

    public static void ExtractIni1(in SharedRef<IStorage> iniStorage, string outDir)
    {
        using var ini1 = new InitialProcessBinaryReader();
        ini1.Initialize(iniStorage).ThrowIfFailure();

        Directory.CreateDirectory(outDir);
        using var kipReader = new KipReader();

        for (int i = 0; i < ini1.ProcessCount; i++)
        {
            using var kipStorage = new UniqueRef<IStorage>();
            ini1.OpenKipStorage(ref kipStorage.Ref, i).ThrowIfFailure();

            using SharedRef<IStorage> sharedKipStorage = SharedRef<IStorage>.Create(ref kipStorage.Ref);
            kipReader.Initialize(in sharedKipStorage).ThrowIfFailure();

            sharedKipStorage.Get.WriteAllBytes(System.IO.Path.Combine(outDir, $"{kipReader.Name.ToString()}.kip1"));
        }
    }
}