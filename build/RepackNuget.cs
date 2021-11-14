using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using ICSharpCode.SharpZipLib.Zip;
using Nuke.Common.IO;
using Nuke.Common.Tools.NuGet;
using static Nuke.Common.IO.FileSystemTasks;

namespace LibHacBuild;

public partial class Build
{
    public void RepackNugetPackage(string path)
    {
        AbsolutePath tempDir = TempDirectory / Path.GetFileName(path);
        AbsolutePath libDir = tempDir / "lib";
        AbsolutePath relsFile = tempDir / "_rels" / ".rels";

        try
        {
            EnsureCleanDirectory(tempDir);
            List<string> fileList = UnzipPackage(path, tempDir);

            string newPsmdcpName = CalcPsmdcpName(libDir);
            string newPsmdcpPath = RenamePsmdcp(tempDir, newPsmdcpName);
            EditManifestRelationships(relsFile, newPsmdcpPath);

            int index = fileList.FindIndex(x => x.Contains(".psmdcp"));
            fileList[index] = newPsmdcpPath;

            IEnumerable<string> files = Directory.EnumerateFiles(tempDir, "*.json", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(tempDir, "*.xml", SearchOption.AllDirectories))
                .Concat(Directory.EnumerateFiles(tempDir, "*.rels", SearchOption.AllDirectories))
                .Concat(Directory.EnumerateFiles(tempDir, "*.psmdcp", SearchOption.AllDirectories))
                .Concat(Directory.EnumerateFiles(tempDir, "*.nuspec", SearchOption.AllDirectories));

            foreach (string filename in files)
            {
                Console.WriteLine(filename);
                ReplaceLineEndings(filename);
            }

            ZipDirectory(path, tempDir, fileList);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    public List<string> UnzipPackage(string package, string dest)
    {
        var fileList = new List<string>();

        UnzipFiles(package, dest);

        using (var s = new ZipInputStream(File.OpenRead(package)))
        {
            ZipEntry entry;
            while ((entry = s.GetNextEntry()) != null)
            {
                fileList.Add(entry.Name);
            }
        }

        return fileList;
    }

    public static string CalcPsmdcpName(string libDir)
    {
        using (var sha = SHA256.Create())
        {
            foreach (string file in Directory.EnumerateFiles(libDir))
            {
                byte[] data = File.ReadAllBytes(file);
                sha.TransformBlock(data, 0, data.Length, data, 0);
            }

            sha.TransformFinalBlock(new byte[0], 0, 0);

            return ToHexString(sha.Hash).ToLower().Substring(0, 32);
        }
    }

    public static string RenamePsmdcp(string packageDir, string name)
    {
        string fileName = Directory.EnumerateFiles(packageDir, "*.psmdcp", SearchOption.AllDirectories).Single();
        string newFileName = Path.Combine(Path.GetDirectoryName(fileName), name + ".psmdcp");
        Directory.Move(fileName, newFileName);

        return Path.GetRelativePath(packageDir, newFileName).Replace('\\', '/');
    }

    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    public void EditManifestRelationships(string path, string psmdcpPath)
    {
        XDocument doc = XDocument.Load(path);
        XNamespace ns = doc.Root.GetDefaultNamespace();

        foreach (XElement rs in doc.Root.Elements(ns + "Relationship"))
        {
            using (var sha = SHA256.Create())
            {
                if (rs.Attribute("Target").Value.Contains(".psmdcp"))
                {
                    rs.Attribute("Target").Value = "/" + psmdcpPath;
                }

                string s = "/" + psmdcpPath + rs.Attribute("Target").Value;
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
                string id = "R" + ToHexString(hash).Substring(0, 16);
                rs.Attribute("Id").Value = id;
            }
        }

        doc.Save(path);
    }

    public void SignNupkg(string pkgPath, string password)
    {
        NuGetTasks.NuGet(
            $"sign \"{pkgPath}\" -CertificatePath cert.pfx -CertificatePassword {password} -Timestamper http://timestamp.digicert.com",
            outputFilter: x => x.Replace(password, "hunter2"));
    }

    public static string ToHexString(byte[] arr)
    {
        return BitConverter.ToString(arr).ToLower().Replace("-", "");
    }
}
