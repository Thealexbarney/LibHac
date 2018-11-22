using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using ICSharpCode.SharpZipLib.Zip;
using ILRepacking;
using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.Results);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly string Configuration = IsLocalBuild ? "Debug" : "Release";

    [Solution("LibHac.sln")] readonly Solution Solution;
    [GitRepository] readonly GitRepository GitRepository;

    AbsolutePath SourceDirectory => RootDirectory / "src";
    AbsolutePath TestsDirectory => RootDirectory / "tests";
    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    AbsolutePath TempDirectory => RootDirectory / ".tmp";
    AbsolutePath CliCoreDir => TempDirectory / "hactoolnet_netcoreapp2.1";
    AbsolutePath CliFrameworkDir => TempDirectory / "hactoolnet_net46";
    AbsolutePath CliFrameworkZip => ArtifactsDirectory / "hactoolnet.zip";
    AbsolutePath CliCoreZip => ArtifactsDirectory / "hactoolnet_netcore.zip";

    AbsolutePath CliMergedExe => ArtifactsDirectory / "hactoolnet.exe";

    Project LibHacProject => Solution.GetProject("LibHac").NotNull();
    Project HactoolnetProject => Solution.GetProject("hactoolnet").NotNull();

    Target Clean => _ => _
        .Executes(() =>
        {
            DeleteDirectories(GlobDirectories(SourceDirectory, "**/bin", "**/obj"));
            DeleteDirectories(GlobDirectories(TestsDirectory, "**/bin", "**/obj"));
            EnsureCleanDirectory(ArtifactsDirectory);
            EnsureCleanDirectory(CliCoreDir);
            EnsureCleanDirectory(CliFrameworkDir);
        });

    Target Restore => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            var settings = new DotNetRestoreSettings()
                .SetProjectFile(Solution);

            if (EnvironmentInfo.IsUnix) settings = settings.RemoveRuntimes("net46");

            DotNetRestore(s => settings);
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            var buildSettings = new DotNetBuildSettings()
                .SetProjectFile(Solution)
                .EnableNoRestore()
                .SetConfiguration(Configuration);

            if (EnvironmentInfo.IsUnix) buildSettings = buildSettings.SetFramework("netcoreapp2.1");

            DotNetBuild(s => buildSettings);

            var publishSettings = new DotNetPublishSettings()
                .EnableNoRestore()
                .SetConfiguration(Configuration);

            DotNetPublish(s => publishSettings
                .SetProject(HactoolnetProject)
                .SetFramework("netcoreapp2.1")
                .SetOutput(CliCoreDir));

            if (EnvironmentInfo.IsWin)
            {
                DotNetPublish(s => publishSettings
                    .SetProject(HactoolnetProject)
                    .SetFramework("net46")
                    .SetOutput(CliFrameworkDir));
            }

            // Hack around OS newline differences
            if (EnvironmentInfo.IsUnix)
            {
                foreach (string filename in Directory.EnumerateFiles(CliCoreDir, "*.json"))
                {
                    ReplaceLineEndings(filename);
                }
            }
        });

    Target Pack => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            var settings = new DotNetPackSettings()
                .SetProject(LibHacProject)
                .EnableNoBuild()
                .SetConfiguration(Configuration)
                .EnableIncludeSymbols()
                .SetOutputDirectory(ArtifactsDirectory);

            if (EnvironmentInfo.IsUnix)
                settings = settings.SetProperties(
                    new Dictionary<string, object> { ["TargetFrameworks"] = "netcoreapp2.1" });

            DotNetPack(s => settings);
        });

    Target Merge => _ => _
        .DependsOn(Compile)
        .OnlyWhen(() => EnvironmentInfo.IsWin)
        .Executes(() =>
        {
            string[] libraries = Directory.GetFiles(CliFrameworkDir, "*.dll");
            var cliList = new List<string> { CliFrameworkDir / "hactoolnet.exe" };
            cliList.AddRange(libraries);

            var cliOptions = new RepackOptions
            {
                OutputFile = CliMergedExe,
                InputAssemblies = cliList.ToArray(),
                SearchDirectories = new[] { "." }
            };

            new ILRepack(cliOptions).Repack();
        });

    Target Zip => _ => _
        .DependsOn(Pack)
        .Executes(() =>
        {
            string[] namesFx = Directory.EnumerateFiles(CliFrameworkDir, "*.exe")
                .Concat(Directory.EnumerateFiles(CliFrameworkDir, "*.dll"))
                .ToArray();

            string[] namesCore = Directory.EnumerateFiles(CliCoreDir, "*.json")
                .Concat(Directory.EnumerateFiles(CliCoreDir, "*.dll"))
                .ToArray();

            if (EnvironmentInfo.IsWin) ZipFiles(CliFrameworkZip, namesFx);
            ZipFiles(CliCoreZip, namesCore);

            if (Host == HostType.AppVeyor)
            {
                PushArtifact(CliFrameworkZip);
                PushArtifact(CliCoreZip);
                PushArtifact(CliMergedExe);
            }
        });

    Target Results => _ => _
        .DependsOn(Zip, Merge)
        .Executes(() =>
        {
            Console.WriteLine("SHA-1:");
            using (SHA1 sha = SHA1.Create())
            {
                foreach (string filename in Directory.EnumerateFiles(ArtifactsDirectory))
                {
                    using (var stream = new FileStream(filename, FileMode.Open))
                    {
                        string hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                        Console.WriteLine($"{hash} - {Path.GetFileName(filename)}");
                    }
                }
            }
        });

    public static void ZipFiles(string outFile, string[] files)
    {
        using (var s = new ZipOutputStream(File.Create(outFile)))
        {
            s.SetLevel(9);

            foreach (string file in files)
            {
                var entry = new ZipEntry(Path.GetFileName(file));
                entry.DateTime = DateTime.UnixEpoch;

                using (FileStream fs = File.OpenRead(file))
                {
                    entry.Size = fs.Length;
                    s.PutNextEntry(entry);
                    fs.CopyTo(s);
                }
            }
        }
    }

    public static void PushArtifact(string path)
    {
        if (!File.Exists(path))
        {

            Console.WriteLine(path);
        }

        var psi = new ProcessStartInfo
        {
            FileName = "appveyor",
            Arguments = $"PushArtifact \"{path}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        Process proc = new Process
        {
            StartInfo = psi
        };

        proc.Start();

        proc.WaitForExit();
    }

    public static void ReplaceLineEndings(string filename)
    {
        string text = File.ReadAllText(filename);
        File.WriteAllText(filename, text.Replace("\n", "\r\n"));
    }
}
