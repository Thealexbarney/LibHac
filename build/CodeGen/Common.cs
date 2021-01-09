using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Nuke.Common;

namespace LibHacBuild.CodeGen
{
    public static class Common
    {
        public static string GetHeader()
        {
            string nl = Environment.NewLine;
            return
                "//-----------------------------------------------------------------------------" + nl +
                "// This file was automatically generated." + nl +
                "// Changes to this file will be lost when the file is regenerated." + nl +
                "//" + nl +
                "// To change this file, modify /build/CodeGen/results.csv at the root of this" + nl +
                "// repo and run the build script." + nl +
                "//" + nl +
                "// The script can be run with the \"codegen\" option to run only the" + nl +
                "// code generation portion of the build." + nl +
                "//-----------------------------------------------------------------------------";
        }

        // Write the file only if it has changed
        // Preserve the UTF-8 BOM usage if the file already exists
        public static void WriteOutput(string relativePath, string text)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return;

            string rootPath = FindProjectDirectory();
            string fullPath = Path.Combine(rootPath, relativePath);
            string directoryName = Path.GetDirectoryName(fullPath);

            if (directoryName == null)
                throw new InvalidDataException($"Invalid output path {relativePath}");

            if (!Directory.Exists(directoryName))
            {
                Directory.CreateDirectory(directoryName);
            }

            // Default is true because Visual Studio saves .cs files with the BOM by default
            bool hasBom = true;
            byte[] bom = Encoding.UTF8.GetPreamble();
            byte[] oldFile = null;

            if (File.Exists(fullPath))
            {
                oldFile = File.ReadAllBytes(fullPath);

                if (oldFile.Length >= 3)
                    hasBom = oldFile.AsSpan(0, 3).SequenceEqual(bom);
            }

            // Make line endings the same on Windows and Unix
            if (Environment.NewLine == "\n")
            {
                text = text.Replace("\n", "\r\n");
            }

            byte[] newFile = (hasBom ? bom : new byte[0]).Concat(Encoding.UTF8.GetBytes(text)).ToArray();

            if (oldFile?.SequenceEqual(newFile) == true)
            {
                Logger.Normal($"{relativePath} is already up-to-date");
                return;
            }

            Logger.Normal($"Generated file {relativePath}");
            File.WriteAllBytes(fullPath, newFile);
        }

        public static Stream GetResource(string name)
        {
            var assembly = Assembly.GetExecutingAssembly();
            string path = $"LibHacBuild.CodeGen.{name}";

            Stream stream = assembly.GetManifestResourceStream(path);
            if (stream == null) throw new FileNotFoundException($"Resource {path} was not found.");

            return stream;
        }

        public static string FindProjectDirectory()
        {
            string currentDir = Environment.CurrentDirectory;

            while (currentDir != null)
            {
                if (File.Exists(Path.Combine(currentDir, "LibHac.sln")))
                {
                    break;
                }

                currentDir = Path.GetDirectoryName(currentDir);
            }

            if (currentDir == null)
                throw new DirectoryNotFoundException("Unable to find project directory.");

            return Path.Combine(currentDir, "src");
        }
    }
}
