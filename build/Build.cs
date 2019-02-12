using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ICSharpCode.SharpZipLib.Zip;
using ILRepacking;
using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.SignTool;
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
    AbsolutePath SignedArtifactsDirectory => ArtifactsDirectory / "signed";
    AbsolutePath TempDirectory => RootDirectory / ".tmp";
    AbsolutePath CliCoreDir => TempDirectory / "hactoolnet_netcoreapp2.1";
    AbsolutePath CliFrameworkDir => TempDirectory / "hactoolnet_net46";
    AbsolutePath CliFrameworkZip => ArtifactsDirectory / "hactoolnet.zip";
    AbsolutePath CliCoreZip => ArtifactsDirectory / "hactoolnet_netcore.zip";

    AbsolutePath CliMergedExe => ArtifactsDirectory / "hactoolnet.exe";

    Project LibHacProject => Solution.GetProject("LibHac").NotNull();
    Project LibHacTestProject => Solution.GetProject("LibHac.Tests").NotNull();
    Project HactoolnetProject => Solution.GetProject("hactoolnet").NotNull();

    const string CertFileName = "cert.pfx";

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
            DotNetRestoreSettings settings = new DotNetRestoreSettings()
                .SetProjectFile(Solution);

            DotNetRestore(s => settings);
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuildSettings buildSettings = new DotNetBuildSettings()
                .SetProjectFile(Solution)
                .EnableNoRestore()
                .SetConfiguration(Configuration);

            if (EnvironmentInfo.IsUnix) buildSettings = buildSettings.SetFramework("netcoreapp2.1");

            DotNetBuild(s => buildSettings);

            DotNetPublishSettings publishSettings = new DotNetPublishSettings()
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
            DotNetPackSettings settings = new DotNetPackSettings()
                .SetProject(LibHacProject)
                .EnableNoBuild()
                .SetConfiguration(Configuration)
                .EnableIncludeSymbols()
                .SetSymbolPackageFormat(DotNetSymbolPackageFormat.snupkg)
                .SetOutputDirectory(ArtifactsDirectory);

            if (EnvironmentInfo.IsUnix)
                settings = settings.SetProperties(
                    new Dictionary<string, object> { ["TargetFrameworks"] = "netcoreapp2.1" });

            DotNetPack(s => settings);

            if (Host != HostType.AppVeyor) return;

            foreach (string filename in Directory.EnumerateFiles(ArtifactsDirectory, "*.nupkg"))
            {
                PushArtifact(filename);
            }
        });

    Target Merge => _ => _
        .DependsOn(Compile)
        .OnlyWhenStatic(() => EnvironmentInfo.IsWin)
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

            if (Host == HostType.AppVeyor)
            {
                PushArtifact(CliMergedExe);
            }
        });

    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetTestSettings settings = new DotNetTestSettings()
                .SetProjectFile(LibHacTestProject)
                .EnableNoBuild()
                .SetConfiguration(Configuration);

            if (EnvironmentInfo.IsUnix) settings = settings.SetFramework("netcoreapp2.1");

            DotNetTest(s => settings);
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

            if (EnvironmentInfo.IsWin)
            {
                ZipFiles(CliFrameworkZip, namesFx);
                Console.WriteLine($"Created {CliFrameworkZip}");
            }

            ZipFiles(CliCoreZip, namesCore);
            Console.WriteLine($"Created {CliCoreZip}");

            if (Host == HostType.AppVeyor)
            {
                PushArtifact(CliFrameworkZip);
                PushArtifact(CliCoreZip);
                PushArtifact(CliMergedExe);
            }
        });

    Target Results => _ => _
        .DependsOn(Test, Zip, Merge, Sign)
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

    Target Sign => _ => _
        .DependsOn(Test, Zip, Merge)
        .OnlyWhenStatic(() => EnvironmentInfo.IsWin)
        .OnlyWhenStatic(() => File.Exists(CertFileName))
        .Executes(() =>
        {
            string pwd = ReadPassword();

            if (pwd == string.Empty)
            {
                Console.WriteLine("Skipping sign task");
                return;
            }

            SignAndReZip(pwd);
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

    public static void ZipDirectory(string outFile, string directory)
    {
        using (var s = new ZipOutputStream(File.Create(outFile)))
        {
            s.SetLevel(9);

            foreach (string filePath in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(directory, filePath);

                var entry = new ZipEntry(relativePath);
                entry.DateTime = DateTime.UnixEpoch;

                using (FileStream fs = File.OpenRead(filePath))
                {
                    entry.Size = fs.Length;
                    s.PutNextEntry(entry);
                    fs.CopyTo(s);
                }
            }
        }
    }

    public static void UnzipFiles(string zipFile, string outDir)
    {
        using (var s = new ZipInputStream(File.OpenRead(zipFile)))
        {
            ZipEntry entry;
            while ((entry = s.GetNextEntry()) != null)
            {
                string outPath = Path.Combine(outDir, entry.Name);

                string directoryName = Path.GetDirectoryName(outPath);
                string fileName = Path.GetFileName(outPath);

                if (!string.IsNullOrWhiteSpace(directoryName))
                {
                    Directory.CreateDirectory(directoryName);
                }

                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    using (FileStream outFile = File.Create(outPath))
                    {
                        s.CopyTo(outFile);
                    }
                }
            }
        }
    }

    public static void PushArtifact(string path)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"Unable to add artifact {path}");
        }

        var psi = new ProcessStartInfo
        {
            FileName = "appveyor",
            Arguments = $"PushArtifact \"{path}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        var proc = new Process
        {
            StartInfo = psi
        };

        proc.Start();

        proc.WaitForExit();

        Console.WriteLine($"Added AppVeyor artifact {path}");
    }

    public static void ReplaceLineEndings(string filename)
    {
        string text = File.ReadAllText(filename);
        File.WriteAllText(filename, text.Replace("\n", "\r\n"));
    }

    public static void SignAssemblies(string password, params string[] fileNames)
    {
        SignToolSettings settings = new SignToolSettings()
            .SetFileDigestAlgorithm("SHA256")
            .SetFile(CertFileName)
            .SetFiles(fileNames)
            .SetPassword(password)
            .SetTimestampServerDigestAlgorithm("SHA256")
            .SetRfc3161TimestampServerUrl("http://timestamp.digicert.com")
            ;

        SignToolTasks.SignTool(settings);
    }

    public void SignAndReZip(string password)
    {
        AbsolutePath nupkgFile = ArtifactsDirectory.GlobFiles("*.nupkg").First();
        AbsolutePath nupkgDir = ArtifactsDirectory / $"{Path.GetFileName(nupkgFile)}_extract";
        AbsolutePath netFxDir = ArtifactsDirectory / Path.GetFileNameWithoutExtension(CliFrameworkZip);
        AbsolutePath coreFxDir = ArtifactsDirectory / Path.GetFileNameWithoutExtension(CliCoreZip);
        AbsolutePath signedMergedExe = SignedArtifactsDirectory / Path.GetFileName(CliMergedExe);

        try
        {
            UnzipFiles(CliFrameworkZip, netFxDir);
            UnzipFiles(CliCoreZip, coreFxDir);
            UnzipFiles(nupkgFile, nupkgDir);

            var toSign = new List<AbsolutePath>();
            toSign.AddRange(nupkgDir.GlobFiles("**/LibHac.dll"));
            toSign.Add(netFxDir / "hactoolnet.exe");
            toSign.Add(coreFxDir / "hactoolnet.dll");
            toSign.Add(signedMergedExe);

            Directory.CreateDirectory(SignedArtifactsDirectory);
            File.Copy(CliMergedExe, signedMergedExe, true);

            ZipDirectory(SignedArtifactsDirectory / Path.GetFileName(nupkgFile), nupkgDir);
            ZipDirectory(SignedArtifactsDirectory / Path.GetFileName(CliFrameworkZip), netFxDir);
            ZipDirectory(SignedArtifactsDirectory / Path.GetFileName(CliCoreZip), coreFxDir);

            SignAssemblies(password, toSign.Select(x => x.ToString()).ToArray());

            // Avoid having multiple signed versions of the same file
            File.Copy(nupkgDir / "lib" / "net46" / "LibHac.dll", netFxDir / "LibHac.dll", true);
            File.Copy(nupkgDir / "lib" / "netcoreapp2.1" / "LibHac.dll", coreFxDir / "LibHac.dll", true);

            ZipDirectory(SignedArtifactsDirectory / Path.GetFileName(nupkgFile), nupkgDir);
            ZipDirectory(SignedArtifactsDirectory / Path.GetFileName(CliFrameworkZip), netFxDir);
            ZipDirectory(SignedArtifactsDirectory / Path.GetFileName(CliCoreZip), coreFxDir);
        }
        catch (Exception)
        {
            Directory.Delete(SignedArtifactsDirectory, true);
            throw;
        }
        finally
        {
            Directory.Delete(nupkgDir, true);
            Directory.Delete(netFxDir, true);
            Directory.Delete(coreFxDir, true);
        }
    }

    public static string ReadPassword()
    {
        var pwd = new StringBuilder();
        ConsoleKeyInfo key;

        Console.Write("Enter certificate password (Empty password to skip): ");
        do
        {
            key = Console.ReadKey(true);

            // Ignore any key out of range.
            if (((int)key.Key) >= '!' && ((int)key.Key <= '~'))
            {
                // Append the character to the password.
                pwd.Append(key.KeyChar);
                Console.Write("*");
            }
            // Exit if Enter key is pressed.
        } while (key.Key != ConsoleKey.Enter);
        Console.WriteLine();

        return pwd.ToString();
    }
}
