using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CsvHelper;
using Nuke.Common;

namespace LibHacBuild.CodeGen
{
    public static class ResultCodeGen
    {
        // RyuJIT will always be inlined a function if its CIL size is <= 0x10 bytes
        private const int InlineThreshold = 0x10;

        public static void Run()
        {
            ModuleInfo[] modules = ReadResults();

            SetEmptyResultNames(modules);
            CheckIfAggressiveInliningNeeded(modules);
            SetOutputPaths(modules);

            foreach (ModuleInfo module in modules)
            {
                string moduleResultFile = PrintModule(module);

                WriteOutput(module, moduleResultFile);
            }
        }

        private static ModuleInfo[] ReadResults()
        {
            ModuleIndex[] moduleNames = ReadCsv<ModuleIndex>("result_modules.csv");
            ModulePath[] modulePaths = ReadCsv<ModulePath>("result_paths.csv");
            ResultInfo[] results = ReadCsv<ResultInfo>("results.csv");

            var modules = new Dictionary<string, ModuleInfo>();

            foreach (ModuleIndex name in moduleNames)
            {
                var module = new ModuleInfo();
                module.Name = name.Name;
                module.Index = name.Index;

                modules.Add(name.Name, module);
            }

            foreach (ModulePath path in modulePaths)
            {
                ModuleInfo module = modules[path.Name];
                module.Namespace = path.Namespace;
                module.Path = path.Path;
            }

            foreach (ModuleInfo module in modules.Values)
            {
                module.Results = results.Where(x => x.Module == module.Index).OrderBy(x => x.DescriptionStart)
                    .ToArray();
            }

            return modules.Values.ToArray();
        }

        private static void SetEmptyResultNames(ModuleInfo[] modules)
        {
            foreach (ModuleInfo module in modules)
            {
                foreach (ResultInfo result in module.Results.Where(x => string.IsNullOrWhiteSpace(x.Name)))
                {
                    if (result.DescriptionEnd.HasValue)
                    {
                        result.Name += $"Range{result.DescriptionStart}To{result.DescriptionEnd}";
                    }
                    else
                    {
                        result.Name = $"Result{result.DescriptionStart}";
                    }
                }
            }
        }

        private static void CheckIfAggressiveInliningNeeded(ModuleInfo[] modules)
        {
            foreach (ModuleInfo module in modules)
            {
                module.NeedsAggressiveInlining = module.Results.Any(x => EstimateCilSize(x) > InlineThreshold);
            }
        }

        private static void SetOutputPaths(ModuleInfo[] modules)
        {
            string rootPath = FindProjectDirectory();

            foreach (ModuleInfo module in modules.Where(x => !string.IsNullOrWhiteSpace(x.Path)))
            {
                module.FullPath = Path.Combine(rootPath, module.Path);
            }
        }

        private static string PrintModule(ModuleInfo module)
        {
            var sb = new IndentingStringBuilder();

            if (module.NeedsAggressiveInlining)
            {
                sb.AppendLine("using System.Runtime.CompilerServices;");
                sb.AppendLine();
            }

            sb.AppendLine($"namespace {module.Namespace}");
            sb.AppendLineAndIncrease("{");

            sb.AppendLine($"public static class Result{module.Name}");
            sb.AppendLineAndIncrease("{");

            sb.AppendLine($"public const int Module{module.Name} = {module.Index};");
            sb.AppendLine();

            foreach (ResultInfo result in module.Results)
            {
                PrintResult(sb, module.Name, result);
            }

            sb.DecreaseAndAppendLine("}");
            sb.DecreaseAndAppendLine("}");

            return sb.ToString();
        }

        private static void PrintResult(IndentingStringBuilder sb, string moduleName, ResultInfo result)
        {
            string descriptionArgs;

            if (result.DescriptionEnd.HasValue)
            {
                descriptionArgs = $"{result.DescriptionStart}, {result.DescriptionEnd}";
            }
            else
            {
                descriptionArgs = $"{result.DescriptionStart}";
            }

            // sb.AppendLine($"/// <summary>Error code: {result.ErrorCode}; Inner value: 0x{result.InnerValue:x}</summary>");

            string resultCtor = $"new Result.Base(Module{moduleName}, {descriptionArgs});";
            sb.Append($"public static Result.Base {result.Name} ");

            if (EstimateCilSize(result) > InlineThreshold)
            {
                sb.AppendLine($"{{ [MethodImpl(MethodImplOptions.AggressiveInlining)] get => {resultCtor} }}");
            }
            else
            {
                sb.AppendLine($"=> {resultCtor}");
            }
        }

        // Write the file only if it has changed
        // Preserve the UTF-8 BOM usage if the file already exists
        private static void WriteOutput(ModuleInfo module, string text)
        {
            if (string.IsNullOrWhiteSpace(module.FullPath))
                return;

            // Default is true because Visual Studio saves .cs files with the BOM by default
            bool hasBom = true;
            byte[] bom = Encoding.UTF8.GetPreamble();
            byte[] oldFile = null;

            if (File.Exists(module.FullPath))
            {
                oldFile = File.ReadAllBytes(module.FullPath);

                if (oldFile.Length >= 3)
                    hasBom = oldFile.AsSpan(0, 3).SequenceEqual(bom);
            }

            byte[] newFile = (hasBom ? bom : new byte[0]).Concat(Encoding.UTF8.GetBytes(text)).ToArray();

            if (oldFile?.SequenceEqual(newFile) == true)
            {
                Logger.Normal($"{module.Path} is already up-to-date");
                return;
            }

            Logger.Normal($"Generated file {module.Path}");
            File.WriteAllBytes(module.FullPath, newFile);
        }

        private static T[] ReadCsv<T>(string name)
        {
            using (var csv = new CsvReader(new StreamReader(GetResource(name)), CultureInfo.InvariantCulture))
            {
                csv.Configuration.AllowComments = true;

                return csv.GetRecords<T>().ToArray();
            }
        }

        private static Stream GetResource(string name)
        {
            var assembly = Assembly.GetExecutingAssembly();
            string path = $"LibHacBuild.CodeGen.{name}";

            Stream stream = assembly.GetManifestResourceStream(path);
            if (stream == null) throw new FileNotFoundException($"Resource {path} was not found.");

            return stream;
        }

        private static string FindProjectDirectory()
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

        private static int EstimateCilSize(ResultInfo result)
        {
            int size = 0;

            size += GetLoadSize(result.Module);
            size += GetLoadSize(result.DescriptionStart);

            if (result.DescriptionEnd.HasValue)
                size += GetLoadSize(result.DescriptionEnd.Value);

            size += 5; // newobj
            size += 1; // ret

            return size;

            static int GetLoadSize(int value)
            {
                if (value >= -1 && value <= 8)
                    return 1; // ldc.i4.X

                if (value >= sbyte.MinValue && value <= sbyte.MaxValue)
                    return 2; // ldc.i4.s XX

                return 5; // ldc.i4 XXXXXXXX
            }
        }
    }

    public class ModuleIndex
    {
        public string Name { get; set; }
        public int Index { get; set; }
    }

    public class ModulePath
    {
        public string Name { get; set; }
        public string Namespace { get; set; }
        public string Path { get; set; }
    }

    [DebuggerDisplay("{" + nameof(Name) + ",nq}")]
    public class ModuleInfo
    {
        public string Name { get; set; }
        public int Index { get; set; }
        public string Namespace { get; set; }
        public string Path { get; set; }

        public string FullPath { get; set; }
        public bool NeedsAggressiveInlining { get; set; }
        public ResultInfo[] Results { get; set; }
    }

    [DebuggerDisplay("{" + nameof(Name) + ",nq}")]
    public class ResultInfo
    {
        public int Module { get; set; }
        public int DescriptionStart { get; set; }
        public int? DescriptionEnd { get; set; }
        public string Name { get; set; }

        public string ErrorCode => $"{2000 + Module:d4}-{DescriptionStart:d4}";
        public int InnerValue => Module & 0x1ff | ((DescriptionStart & 0x7ffff) << 9);
    }
}
