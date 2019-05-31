using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ICSharpCode.SharpZipLib.Zip;
using ILRepacking;
using Nuke.Common;
using Nuke.Common.BuildServers;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
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
        public static int Main() => Execute<Build>(x => x.Results);

        [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
        public readonly string Configuration = IsLocalBuild ? "Debug" : "Release";

        [Parameter("Build only .NET Core targets if true. Default is false on Windows")]
        public readonly bool DoCoreBuildOnly;

        [Solution("LibHac.sln")] readonly Solution _solution;
        [GitRepository] readonly GitRepository _gitRepository;
        [GitVersion] readonly GitVersion _gitVersion;

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

        Project LibHacProject => _solution.GetProject("LibHac").NotNull();
        Project LibHacTestProject => _solution.GetProject("LibHac.Tests").NotNull();
        Project HactoolnetProject => _solution.GetProject("hactoolnet").NotNull();

        string AppVeyorVersion { get; set; }
        Dictionary<string, object> VersionProps { get; set; } = new Dictionary<string, object>();

        const string CertFileName = "cert.pfx";

        Target SetVersion => _ => _
            .OnlyWhenStatic(() => _gitRepository != null)
            .Executes(() =>
            {
                AppVeyorVersion = $"{_gitVersion.AssemblySemVer}";
                if (!string.IsNullOrWhiteSpace(_gitVersion.PreReleaseTag))
                {
                    AppVeyorVersion += $"-{_gitVersion.PreReleaseTag}+{_gitVersion.Sha.Substring(0, 8)}";
                }

                string suffix = _gitVersion.PreReleaseTag;

                if (!string.IsNullOrWhiteSpace(suffix))
                {
                    if (!_gitRepository.IsOnMasterBranch())
                    {
                        suffix = $"-{suffix}";
                    }

                    suffix += $"+{_gitVersion.Sha.Substring(0, 8)}";
                }

                VersionProps = new Dictionary<string, object>
                {
                    ["VersionPrefix"] = _gitVersion.AssemblySemVer,
                    ["VersionSuffix"] = suffix
                };

                Console.WriteLine($"Building version {AppVeyorVersion}");

                if (Host == HostType.AppVeyor)
                {
                    SetAppVeyorVersion(AppVeyorVersion);
                }
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
                EnsureCleanDirectory(CliFrameworkDir);
            });

        Target Restore => _ => _
            .DependsOn(Clean)
            .Executes(() =>
            {
                DotNetRestoreSettings settings = new DotNetRestoreSettings()
                    .SetProjectFile(_solution);

                DotNetRestore(s => settings);
            });

        Target Compile => _ => _
            .DependsOn(Restore, SetVersion)
            .Executes(() =>
            {
                DotNetBuildSettings buildSettings = new DotNetBuildSettings()
                    .SetProjectFile(_solution)
                    .EnableNoRestore()
                    .SetConfiguration(Configuration)
                    .SetProperties(VersionProps)
                    .SetProperty("BuildType", "Release");

                if (DoCoreBuildOnly) buildSettings = buildSettings.SetFramework("netcoreapp2.1");

                DotNetBuild(s => buildSettings);

                DotNetPublishSettings publishSettings = new DotNetPublishSettings()
                    .EnableNoRestore()
                    .SetConfiguration(Configuration);

                DotNetPublish(s => publishSettings
                    .SetProject(HactoolnetProject)
                    .SetFramework("netcoreapp2.1")
                    .SetOutput(CliCoreDir)
                    .SetProperties(VersionProps));

                if (!DoCoreBuildOnly)
                {
                    DotNetPublish(s => publishSettings
                        .SetProject(HactoolnetProject)
                        .SetFramework("net46")
                        .SetOutput(CliFrameworkDir)
                        .SetProperties(VersionProps));
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
                    .SetOutputDirectory(ArtifactsDirectory)
                    .SetProperties(VersionProps);

                if (DoCoreBuildOnly)
                    settings = settings.SetProperty("TargetFrameworks", "netcoreapp2.1");

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

        Target Merge => _ => _
            .DependsOn(Compile)
            .OnlyWhenStatic(() => !DoCoreBuildOnly)
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

                foreach (AbsolutePath file in ArtifactsDirectory.GlobFiles("*.exe.config"))
                {
                    File.Delete(file);
                }

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

                if (DoCoreBuildOnly) settings = settings.SetFramework("netcoreapp2.1");

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

                if (!DoCoreBuildOnly)
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

        Target Publish => _ => _
            .DependsOn(Test)
            .OnlyWhenStatic(() => Host == HostType.AppVeyor)
            .OnlyWhenStatic(() => AppVeyor.Instance != null && AppVeyor.Instance.PullRequestTitle == null)
            .Executes(() =>
            {
                AbsolutePath nupkgFile = ArtifactsDirectory.GlobFiles("*.nupkg").Single();
                AbsolutePath snupkgFile = ArtifactsDirectory.GlobFiles("*.snupkg").Single();

                string apiKey = EnvironmentInfo.Variable("myget_api_key");
                DotNetNuGetPushSettings settings = new DotNetNuGetPushSettings()
                    .SetApiKey(apiKey)
                    .SetSymbolApiKey(apiKey)
                    .SetSource("https://www.myget.org/F/libhac/api/v2/package")
                    .SetSymbolSource("https://www.myget.org/F/libhac/symbols/api/v2/package");

                DotNetNuGetPush(settings.SetTargetPath(nupkgFile));
                DotNetNuGetPush(settings.SetTargetPath(snupkgFile));
            });

        Target Results => _ => _
            .DependsOn(Test, Zip, Merge, Sign, Publish)
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
            .OnlyWhenStatic(() => !DoCoreBuildOnly)
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
            AbsolutePath netFxDir = TempDirectory / ("sign_" + Path.GetFileName(CliFrameworkZip));
            AbsolutePath coreFxDir = TempDirectory / ("sign_" + Path.GetFileName(CliCoreZip));
            AbsolutePath signedMergedExe = SignedArtifactsDirectory / Path.GetFileName(CliMergedExe);

            try
            {
                UnzipFiles(CliFrameworkZip, netFxDir);
                UnzipFiles(CliCoreZip, coreFxDir);
                List<string> pkgFileList = UnzipPackage(nupkgFile, nupkgDir);

                var toSign = new List<AbsolutePath>();
                toSign.AddRange(nupkgDir.GlobFiles("**/LibHac.dll"));
                toSign.Add(netFxDir / "hactoolnet.exe");
                toSign.Add(coreFxDir / "hactoolnet.dll");
                toSign.Add(signedMergedExe);

                Directory.CreateDirectory(SignedArtifactsDirectory);
                File.Copy(CliMergedExe, signedMergedExe, true);

                SignAssemblies(password, toSign.Select(x => x.ToString()).ToArray());

                // Avoid having multiple signed versions of the same file
                File.Copy(nupkgDir / "lib" / "net46" / "LibHac.dll", netFxDir / "LibHac.dll", true);
                File.Copy(nupkgDir / "lib" / "netcoreapp2.1" / "LibHac.dll", coreFxDir / "LibHac.dll", true);

                ZipDirectory(SignedArtifactsDirectory / Path.GetFileName(nupkgFile), nupkgDir, pkgFileList);
                ZipDirectory(SignedArtifactsDirectory / Path.GetFileName(CliFrameworkZip), netFxDir);
                ZipDirectory(SignedArtifactsDirectory / Path.GetFileName(CliCoreZip), coreFxDir);

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
}
