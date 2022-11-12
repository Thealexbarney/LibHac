using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.SharpZipLib.Zip.Compression;
using LibHacBuild.CodeGen.Stage1;
using Nuke.Common;
using Nuke.Common.CI.AppVeyor;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Tools.SignTool;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

namespace LibHacBuild;

partial class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.Standard);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    public readonly string Configuration = IsLocalBuild ? "Debug" : "Release";

    [Parameter("Don't enable any size-reducing settings on native builds.")]
    public readonly bool Untrimmed;

    [Parameter("Disable reflection in native builds.")]
    public readonly bool NoReflection;

    [Solution("LibHac.sln")] readonly Solution _solution;

    AbsolutePath SourceDirectory => RootDirectory / "src";
    AbsolutePath TestsDirectory => RootDirectory / "tests";
    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    AbsolutePath SignedArtifactsDirectory => ArtifactsDirectory / "signed";
    AbsolutePath TempDirectory => RootDirectory / ".nuke" / "temp";
    AbsolutePath CliCoreDir => TempDirectory / "hactoolnet_net6.0";
    AbsolutePath CliNativeDir => TempDirectory / $"hactoolnet_{HostOsName}";
    AbsolutePath CliNativeExe => CliNativeDir / $"hactoolnet{NativeProgramExtension}";
    AbsolutePath CliCoreZip => ArtifactsDirectory / $"hactoolnet-{VersionString}-netcore.zip";
    AbsolutePath CliNativeZip => ArtifactsDirectory / $"hactoolnet-{VersionString}-{HostOsName}.zip";

    Project LibHacProject => _solution.GetProject("LibHac").NotNull();
    Project LibHacTestProject => _solution.GetProject("LibHac.Tests").NotNull();
    Project HactoolnetProject => _solution.GetProject("hactoolnet").NotNull();

    Project CodeGenProject => _solution.GetProject("_buildCodeGen").NotNull();

    private bool HasGitDir { get; set; }

    private string NativeRuntime { get; set; }
    private string HostOsName { get; set; }
    private string NativeProgramExtension { get; set; }

    private DateTimeOffset CommitTime { get; set; } = DateTimeOffset.Now;
    string VersionString { get; set; }
    Dictionary<string, object> VersionProps { get; set; } = new Dictionary<string, object>();

    const string CertFileName = "cert.pfx";

    public Build()
    {
        if (EnvironmentInfo.IsWin)
        {
            NativeRuntime = "win-x64";
            NativeProgramExtension = ".exe";
            HostOsName = "win";
        }
        else if (EnvironmentInfo.IsLinux)
        {
            NativeRuntime = "linux-x64";
            NativeProgramExtension = "";
            HostOsName = "linux";
        }
        else if (EnvironmentInfo.IsOsx)
        {
            NativeRuntime = "osx-x64";
            NativeProgramExtension = "";
            HostOsName = "macos";
        }
    }

    Target SetVersion => _ => _
        .Executes(() =>
        {
            GitRepository gitRepository = null;
            GitVersion gitVersion = null;

            try
            {
                gitRepository = (GitRepository)new GitRepositoryAttribute().GetValue(null, null);

                gitVersion = GitVersionTasks.GitVersion(s => s
                        .SetFramework("net6.0")
                        .DisableProcessLogOutput())
                    .Result;
            }
            catch (Exception e)
            {
                if (!e.Message.Contains("not a git repository", StringComparison.OrdinalIgnoreCase))
                {
                    Serilog.Log.Error(e, e.Message);
                }
            }

            if (gitRepository == null || gitVersion == null)
            {
                Serilog.Log.Debug("Unable to read Git version.");

                VersionString = GetCsprojVersion();
                Serilog.Log.Debug($"Using version from .csproj: {VersionString}");

                return;
            }

            HasGitDir = true;

            if (DateTimeOffset.TryParseExact(gitVersion.CommitDate, "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture.DateTimeFormat, DateTimeStyles.AssumeUniversal, out DateTimeOffset commitTime))
            {
                CommitTime = commitTime.LocalDateTime;
            }

            VersionString = $"{gitVersion.MajorMinorPatch}";
            if (!string.IsNullOrWhiteSpace(gitVersion.PreReleaseTag))
            {
                VersionString += $"-{gitVersion.PreReleaseTag}+{gitVersion.Sha.Substring(0, 8)}";
            }

            string suffix = gitVersion.PreReleaseTag;

            if (!string.IsNullOrWhiteSpace(suffix))
            {
                if (!gitRepository.IsOnMasterBranch())
                {
                    suffix = $"-{suffix}";
                }

                suffix += $"+{gitVersion.Sha.Substring(0, 8)}";
            }

            if (Host is AppVeyor appVeyor)
            {
                // Workaround GitVersion issue by getting PR info manually https://github.com/GitTools/GitVersion/issues/1927
                int? prNumber = appVeyor.PullRequestNumber;
                string branchName = Environment.GetEnvironmentVariable("APPVEYOR_PULL_REQUEST_HEAD_REPO_BRANCH");

                if (prNumber != null && branchName != null)
                {
                    string prString = $"PullRequest{prNumber:D4}";

                    VersionString = VersionString.Replace(branchName, prString);
                    suffix = suffix?.Replace(branchName, prString) ?? "";
                }

                appVeyor.UpdateBuildVersion(VersionString);
            }

            VersionProps = new Dictionary<string, object>
            {
                ["VersionPrefix"] = gitVersion.AssemblySemVer,
                ["VersionSuffix"] = suffix
            };

            Serilog.Log.Debug($"Building version {VersionString}");
        });

    Target Clean => _ => _
        .Executes(() =>
        {
            List<string> toDelete = GlobDirectories(SourceDirectory, "**/bin", "**/obj")
                .Concat(GlobDirectories(TestsDirectory, "**/bin", "**/obj")).ToList();

            foreach (string dir in toDelete)
            {
                DeleteDirectory(dir);
            }

            EnsureCleanDirectory(ArtifactsDirectory);
            EnsureCleanDirectory(CliCoreDir);
            EnsureCleanDirectory(CliNativeDir);
        });

    Target Restore => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            DotNetRestoreSettings settings = new DotNetRestoreSettings()
                .SetProjectFile(_solution);

            DotNetRestore(s => settings);
        });

    Target Codegen => _ => _
        .Executes(() =>
        {
            ResultCodeGen.Run();
            RunCodegenStage2();
        });

    Target Compile => _ => _
        .DependsOn(Restore, SetVersion, Codegen)
        .Executes(() =>
        {
            DotNetBuildSettings buildSettings = new DotNetBuildSettings()
                .SetProjectFile(_solution)
                .EnableNoRestore()
                .SetConfiguration(Configuration)
                .SetProperties(VersionProps)
                .SetProperty("BuildType", "Release")
                .SetProperty("HasGitDir", HasGitDir);

            DotNetBuild(s => buildSettings);

            DotNetPublishSettings publishSettings = new DotNetPublishSettings()
                .EnableNoRestore()
                .SetConfiguration(Configuration);

            DotNetPublish(s => publishSettings
                .SetProject(HactoolnetProject)
                .SetFramework("net6.0")
                .SetOutput(CliCoreDir)
                .SetNoBuild(true)
                .SetProperties(VersionProps));

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
                .SetOutputDirectory(ArtifactsDirectory)
                .SetProperties(VersionProps);

            DotNetPack(s => settings);

            foreach (string filename in Directory.EnumerateFiles(ArtifactsDirectory, "*.*nupkg"))
            {
                RepackNugetPackage(filename);
            }

            foreach (string filename in Directory.EnumerateFiles(ArtifactsDirectory, "*.*nupkg"))
            {
                PushArtifact(filename);
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

            if (EnvironmentInfo.IsUnix) settings = settings.SetProperty("TargetFramework", "net6.0");

            DotNetTest(s => settings);
        });

    Target Zip => _ => _
        .DependsOn(Pack)
        .After(Native)
        .Executes(() =>
        {
            string[] namesCore = Directory.EnumerateFiles(CliCoreDir, "*.json")
                .Concat(Directory.EnumerateFiles(CliCoreDir, "*.dll"))
                .ToArray();

            EnsureExistingDirectory(ArtifactsDirectory);

            ZipFiles(CliCoreZip, namesCore, CommitTime);
            Serilog.Log.Debug($"Created {CliCoreZip}");

            PushArtifact(CliCoreZip);
        });

    Target Publish => _ => _
        .DependsOn(Test, Pack)
        .OnlyWhenStatic(() => AppVeyor.Instance != null && AppVeyor.Instance.PullRequestTitle == null)
        .Executes(() =>
        {
            AbsolutePath nupkgFile = ArtifactsDirectory.GlobFiles("*.nupkg").Single();

            string apiKey = EnvironmentInfo.GetVariable<string>("myget_api_key");
            DotNetNuGetPushSettings settings = new DotNetNuGetPushSettings()
                .SetApiKey(apiKey)
                .SetSource("https://www.myget.org/F/libhac/api/v3/index.json");

            DotNetNuGetPush(settings.SetTargetPath(nupkgFile));
        });

    Target Sign => _ => _
        .DependsOn(Test, Zip)
        .OnlyWhenStatic(() => File.Exists(CertFileName))
        .OnlyWhenStatic(() => EnvironmentInfo.IsWin)
        .WhenSkipped(DependencyBehavior.Execute)
        .Executes(() =>
        {
            string pwd = ReadPassword();

            if (pwd == string.Empty)
            {
                Serilog.Log.Debug("Skipping sign task");
                return;
            }

            SignAndReZip(pwd);
        });

    Target Native => _ => _
        .DependsOn(SetVersion)
        .After(Compile)
        .Executes(BuildNative);

    // ReSharper disable once UnusedMember.Local
    Target AppVeyorBuild => _ => _
        .DependsOn(Zip, Native, Test, Publish)
        .Unlisted()
        .Executes(PrintResults);

    Target Standard => _ => _
        .DependsOn(Test, Zip)
        .Executes(PrintResults);

    // ReSharper disable once UnusedMember.Local
    Target Full => _ => _
        .DependsOn(Sign, Native)
        .Executes(PrintResults);

    public void PrintResults()
    {
        Serilog.Log.Debug("SHA-1:");
        using (var sha = SHA1.Create())
        {
            foreach (string filename in Directory.EnumerateFiles(ArtifactsDirectory))
            {
                using (var stream = new FileStream(filename, FileMode.Open))
                {
                    string hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                    Serilog.Log.Debug($"{hash} - {Path.GetFileName(filename)}");
                }
            }
        }
    }

    public void BuildNative()
    {
        string buildType = Untrimmed ? "native-untrimmed" : "native";

        if (NoReflection)
        {
            buildType = "native-noreflection";
        }

        DotNetPublishSettings publishSettings = new DotNetPublishSettings()
            .SetConfiguration(Configuration)
            .SetProject(HactoolnetProject)
            .SetRuntime(NativeRuntime)
            .SetSelfContained(true)
            .SetOutput(CliNativeDir)
            .SetProperties(VersionProps)
            .AddProperty("BuildType", buildType);

        DotNetPublish(publishSettings);

        if (EnvironmentInfo.IsUnix && !Untrimmed)
        {
            File.Copy(CliNativeExe, CliNativeExe + "_unstripped", true);
            ProcessTasks.StartProcess("strip", CliNativeExe).AssertZeroExitCode();
        }

        EnsureExistingDirectory(ArtifactsDirectory);

        ZipFile(CliNativeZip, CliNativeExe, $"hactoolnet{NativeProgramExtension}", CommitTime);
        Serilog.Log.Debug($"Created {CliNativeZip}");
        Serilog.Log.Debug($"Created {CliNativeZip}");

        PushArtifact(CliNativeZip);
    }

    public static void ZipFiles(string outFile, IEnumerable<string> files, DateTimeOffset fileDateTime)
    {
        using (var s = new ZipOutputStream(File.Create(outFile)))
        {
            s.SetLevel(9);

            foreach (string file in files)
            {
                var entry = new ZipEntry(Path.GetFileName(file));
                entry.DateTime = fileDateTime.DateTime;

                using (FileStream fs = File.OpenRead(file))
                {
                    entry.Size = fs.Length;
                    s.PutNextEntry(entry);
                    fs.CopyTo(s);
                }
            }
        }
    }

    public static void ZipFile(string outFile, string file, string nameInsideZip, DateTimeOffset fileDateTime)
    {
        using (var s = new ZipOutputStream(File.Create(outFile)))
        {
            s.SetLevel(9);

            var entry = new ZipEntry(nameInsideZip);
            entry.DateTime = fileDateTime.DateTime;

            using (FileStream fs = File.OpenRead(file))
            {
                entry.Size = fs.Length;
                s.PutNextEntry(entry);
                fs.CopyTo(s);
            }
        }
    }

    public static void ZipDirectory(string outFile, string directory, DateTimeOffset fileDateTime)
    {
        using (var s = new ZipOutputStream(File.Create(outFile)))
        {
            s.SetLevel(9);

            foreach (string filePath in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(directory, filePath);

                var entry = new ZipEntry(relativePath);
                entry.DateTime = fileDateTime.DateTime;

                using (FileStream fs = File.OpenRead(filePath))
                {
                    entry.Size = fs.Length;
                    s.PutNextEntry(entry);
                    fs.CopyTo(s);
                }
            }
        }
    }

    public static void ZipDirectory(string outFile, string directory, IEnumerable<string> files, DateTimeOffset fileDateTime)
    {
        using (var s = new ZipOutputStream(File.Create(outFile)))
        {
            s.SetLevel(9);

            foreach (string filePath in files)
            {
                string absolutePath = Path.Combine(directory, filePath);

                var entry = new ZipEntry(filePath);
                entry.DateTime = fileDateTime.DateTime;

                using (FileStream fs = File.OpenRead(absolutePath))
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

    public static byte[] DeflateBytes(byte[] data)
    {
        var s = new Deflater(9, true);
        s.SetInput(data);
        s.Finish();
        byte[] buffer = new byte[data.Length];
        s.Deflate(buffer);

        Debug.Assert(s.IsFinished);

        byte[] compressed = new byte[s.TotalOut];
        Array.Copy(buffer, compressed, compressed.Length);
        return compressed;
    }

    public static void PushArtifact(string path, string name = null)
    {
        if (Host is not AppVeyor appVeyor) return;

        if (!File.Exists(path))
        {
            Serilog.Log.Warning($"Unable to add artifact {path}");
        }

        appVeyor.PushArtifact(path, name);

        Serilog.Log.Debug($"Added AppVeyor artifact {path}");
    }

    public static void ReplaceLineEndings(string filename)
    {
        string text = File.ReadAllText(filename);
        File.WriteAllText(filename, Regex.Replace(text, @"\r\n|\n\r|\n|\r", "\r\n"));
    }

    public static void SignAssemblies(string password, params string[] fileNames)
    {
        SignToolSettings settings = new SignToolSettings()
            .SetFileDigestAlgorithm("SHA256")
            .SetFile(CertFileName)
            .SetFiles(fileNames)
            .SetPassword(password)
            .SetTimestampServerDigestAlgorithm("SHA256")
            .SetRfc3161TimestampServerUrl("http://timestamp.digicert.com");

        SignToolTasks.SignTool(settings);
    }

    public void SignAndReZip(string password)
    {
        AbsolutePath nupkgFile = ArtifactsDirectory.GlobFiles("*.nupkg").Single();
        AbsolutePath snupkgFile = ArtifactsDirectory.GlobFiles("*.snupkg").Single();
        AbsolutePath nupkgDir = TempDirectory / ("sign_" + Path.GetFileName(nupkgFile));
        AbsolutePath coreFxDir = TempDirectory / ("sign_" + Path.GetFileName(CliCoreZip));
        AbsolutePath nativeZipDir = TempDirectory / ("sign_" + Path.GetFileName(CliNativeZip));

        bool signNative = CliNativeExe.FileExists();

        try
        {
            UnzipFiles(CliCoreZip, coreFxDir);
            List<string> pkgFileList = UnzipPackage(nupkgFile, nupkgDir);

            var toSign = new List<AbsolutePath>();
            toSign.AddRange(nupkgDir.GlobFiles("**/LibHac.dll"));
            toSign.Add(coreFxDir / "hactoolnet.dll");

            if (signNative)
            {
                UnzipFiles(CliNativeZip, nativeZipDir);
                toSign.Add(nativeZipDir / "hactoolnet.exe");
            }

            Directory.CreateDirectory(SignedArtifactsDirectory);

            SignAssemblies(password, toSign.Select(x => x.ToString()).ToArray());

            // Avoid having multiple signed versions of the same file
            File.Copy(nupkgDir / "lib" / "net6.0" / "LibHac.dll", coreFxDir / "LibHac.dll", true);

            ZipDirectory(SignedArtifactsDirectory / Path.GetFileName(nupkgFile), nupkgDir, pkgFileList, CommitTime);
            ZipDirectory(SignedArtifactsDirectory / Path.GetFileName(CliCoreZip), coreFxDir, CommitTime);

            if (signNative)
            {
                ZipDirectory(SignedArtifactsDirectory / Path.GetFileName(CliNativeZip), nativeZipDir, CommitTime);
            }

            File.Copy(snupkgFile, SignedArtifactsDirectory / Path.GetFileName(snupkgFile));

            SignNupkg(SignedArtifactsDirectory / Path.GetFileName(nupkgFile), password);
            SignNupkg(SignedArtifactsDirectory / Path.GetFileName(snupkgFile), password);
        }
        catch (Exception)
        {
            Directory.Delete(SignedArtifactsDirectory, true);
            throw;
        }
        finally
        {
            Directory.Delete(nupkgDir, true);
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

    public string GetCsprojVersion()
    {
        return XmlTasks.XmlPeekSingle(LibHacProject.Path, "/Project/PropertyGroup/VersionPrefix", null);
    }

    public void RunCodegenStage2()
    {
        Serilog.Log.Debug("\nBuilding stage 2 codegen project.");

        DotNetRunSettings settings = new DotNetRunSettings()
            .SetProjectFile(CodeGenProject.Path);
        //  .SetLogOutput(false);

        try
        {
            DotNetRun(settings);
            Serilog.Log.Debug("");
        }
        catch (ProcessException)
        {
            Serilog.Log.Error("\nError running stage 2 codegen. Skipping...\n");
        }
    }
}