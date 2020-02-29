using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ICSharpCode.SharpZipLib.Zip;
using LibHacBuild.CodeGen;
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

namespace LibHacBuild
{
    partial class Build : NukeBuild
    {
        public static int Main() => Execute<Build>(x => x.Standard);

        [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
        public readonly string Configuration = IsLocalBuild ? "Debug" : "Release";

        [Parameter("Don't enable any size-reducing settings on native builds.")]
        public readonly bool Untrimmed;

        [Solution("LibHac.sln")] readonly Solution _solution;

        AbsolutePath SourceDirectory => RootDirectory / "src";
        AbsolutePath TestsDirectory => RootDirectory / "tests";
        AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
        AbsolutePath SignedArtifactsDirectory => ArtifactsDirectory / "signed";
        AbsolutePath TempDirectory => RootDirectory / ".tmp";
        AbsolutePath CliCoreDir => TempDirectory / "hactoolnet_netcoreapp3.1";
        AbsolutePath CliNativeDir => TempDirectory / $"hactoolnet_{HostOsName}";
        AbsolutePath CliNativeExe => CliNativeDir / $"hactoolnet{NativeProgramExtension}";
        AbsolutePath CliCoreZip => ArtifactsDirectory / $"hactoolnet-{VersionString}-netcore.zip";
        AbsolutePath CliNativeZip => ArtifactsDirectory / $"hactoolnet-{VersionString}-{HostOsName}.zip";

        Project LibHacProject => _solution.GetProject("LibHac").NotNull();
        Project LibHacTestProject => _solution.GetProject("LibHac.Tests").NotNull();
        Project HactoolnetProject => _solution.GetProject("hactoolnet").NotNull();

        private bool HasGitDir { get; set; }

        private string NativeRuntime { get; set; }
        private string HostOsName { get; set; }
        private string NativeProgramExtension { get; set; }

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
                            .SetFramework("netcoreapp3.1")
                            .DisableLogOutput())
                        .Result;
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }

                if (gitRepository == null || gitVersion == null)
                {
                    Logger.Normal("Unable to read Git version.");
                    return;
                }

                HasGitDir = true;

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

                if (Host == HostType.AppVeyor)
                {
                    // Workaround GitVersion issue by getting PR info manually https://github.com/GitTools/GitVersion/issues/1927
                    string prNumber = Environment.GetEnvironmentVariable("APPVEYOR_PULL_REQUEST_NUMBER");
                    string branchName = Environment.GetEnvironmentVariable("APPVEYOR_PULL_REQUEST_HEAD_REPO_BRANCH");

                    if (int.TryParse(prNumber, out int prInt) && branchName != null)
                    {
                        string prString = $"PullRequest{prInt:D4}";

                        VersionString = VersionString.Replace(branchName, prString);
                        suffix = suffix.Replace(branchName, prString);
                    }

                    SetAppVeyorVersion(VersionString);
                }

                VersionProps = new Dictionary<string, object>
                {
                    ["VersionPrefix"] = gitVersion.AssemblySemVer,
                    ["VersionSuffix"] = suffix
                };

                Logger.Normal($"Building version {VersionString}");
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
                    .SetFramework("netcoreapp3.1")
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

                if (Host != HostType.AppVeyor) return;

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

                if (EnvironmentInfo.IsUnix) settings = settings.SetProperty("TargetFramework", "netcoreapp3.1");

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

                ZipFiles(CliCoreZip, namesCore);
                Logger.Normal($"Created {CliCoreZip}");

                if (Host == HostType.AppVeyor)
                {
                    PushArtifact(CliCoreZip);
                }
            });

        Target Publish => _ => _
            .DependsOn(Test, Pack)
            .OnlyWhenStatic(() => AppVeyor.Instance != null && AppVeyor.Instance.PullRequestTitle == null)
            .Executes(() =>
            {
                AbsolutePath nupkgFile = ArtifactsDirectory.GlobFiles("*.nupkg").Single();
                AbsolutePath snupkgFile = ArtifactsDirectory.GlobFiles("*.snupkg").Single();

                string apiKey = EnvironmentInfo.GetVariable<string>("myget_api_key");
                DotNetNuGetPushSettings settings = new DotNetNuGetPushSettings()
                    .SetApiKey(apiKey)
                    .SetSymbolApiKey(apiKey)
                    .SetSource("https://www.myget.org/F/libhac/api/v2/package")
                    .SetSymbolSource("https://www.myget.org/F/libhac/symbols/api/v2/package");

                DotNetNuGetPush(settings.SetTargetPath(nupkgFile));
                DotNetNuGetPush(settings.SetTargetPath(snupkgFile));
            });

        Target Sign => _ => _
            .DependsOn(Test, Zip)
            .OnlyWhenStatic(() => File.Exists(CertFileName))
            .OnlyWhenStatic(() => EnvironmentInfo.IsWin)
            .Executes(() =>
            {
                string pwd = ReadPassword();

                if (pwd == string.Empty)
                {
                    Logger.Normal("Skipping sign task");
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
            .DependsOn(Zip, Native, Publish)
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
            Logger.Normal("SHA-1:");
            using (var sha = SHA1.Create())
            {
                foreach (string filename in Directory.EnumerateFiles(ArtifactsDirectory))
                {
                    using (var stream = new FileStream(filename, FileMode.Open))
                    {
                        string hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                        Logger.Normal($"{hash} - {Path.GetFileName(filename)}");
                    }
                }
            }
        }

        [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
        public void BuildNative()
        {
            string buildType = Untrimmed ? "native-untrimmed" : "native";

            DotNetPublishSettings publishSettings = new DotNetPublishSettings()
                .SetConfiguration(Configuration)
                .SetProject(HactoolnetProject)
                .SetRuntime(NativeRuntime)
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

            ZipFile(CliNativeZip, CliNativeExe, $"hactoolnet{NativeProgramExtension}");
            Logger.Normal($"Created {CliNativeZip}");

            if (Host == HostType.AppVeyor)
            {
                PushArtifact(CliNativeZip);
            }
        }

        public static void ZipFiles(string outFile, IEnumerable<string> files)
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

        public static void ZipFile(string outFile, string file, string nameInsideZip)
        {
            using (var s = new ZipOutputStream(File.Create(outFile)))
            {
                s.SetLevel(9);

                var entry = new ZipEntry(nameInsideZip);
                entry.DateTime = DateTime.UnixEpoch;

                using (FileStream fs = File.OpenRead(file))
                {
                    entry.Size = fs.Length;
                    s.PutNextEntry(entry);
                    fs.CopyTo(s);
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

        public static void ZipDirectory(string outFile, string directory, IEnumerable<string> files)
        {
            using (var s = new ZipOutputStream(File.Create(outFile)))
            {
                s.SetLevel(9);

                foreach (string filePath in files)
                {
                    string absolutePath = Path.Combine(directory, filePath);

                    var entry = new ZipEntry(filePath);
                    entry.DateTime = DateTime.UnixEpoch;

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

        public static void PushArtifact(string path)
        {
            if (!File.Exists(path))
            {
                Logger.Warn($"Unable to add artifact {path}");
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

            Logger.Normal($"Added AppVeyor artifact {path}");
        }

        public static void SetAppVeyorVersion(string version)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "appveyor",
                Arguments = $"UpdateBuild -Version \"{version}\"",
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

            bool signNative = FileExists(CliNativeExe);

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
                File.Copy(nupkgDir / "lib" / "netcoreapp3.0" / "LibHac.dll", coreFxDir / "LibHac.dll", true);

                ZipDirectory(SignedArtifactsDirectory / Path.GetFileName(nupkgFile), nupkgDir, pkgFileList);
                ZipDirectory(SignedArtifactsDirectory / Path.GetFileName(CliCoreZip), coreFxDir);

                if (signNative)
                {
                    ZipDirectory(SignedArtifactsDirectory / Path.GetFileName(CliNativeZip), nativeZipDir);
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
    }
}
