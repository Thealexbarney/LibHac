using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using static LibHacBuild.CodeGen.Common;

namespace LibHacBuild.CodeGen.Stage1;

public static class ResultCodeGen
{
    // RyuJIT will always be inlined a function if its CIL size is <= 0x10 bytes
    private const int InlineThreshold = 0x10;

    public static void Run()
    {
        ResultSet modules = ReadResults();

        SetEmptyResultValues(modules);
        ValidateResults(modules);
        CheckForDuplicates(modules);
        ValidateHierarchy(modules);
        CheckIfAggressiveInliningNeeded(modules);

        foreach (NamespaceInfo module in modules.Namespaces.Where(x =>
            !string.IsNullOrWhiteSpace(x.Path) && x.Results.Any()))
        {
            string moduleResultFile = PrintModule(module);

            WriteOutput($"LibHac/{module.Path}", moduleResultFile);
        }

        byte[] archive = BuildArchive(modules);
        byte[] compressedArchive = Build.DeflateBytes(archive);
        string archiveStr = PrintArchive(compressedArchive);
        WriteOutput("LibHac/Common/ResultNameResolver.Generated.cs", archiveStr);

        string enumStr = PrintEnum(modules);
        WriteOutput("../.nuke/temp/result_enums.txt", enumStr);
    }

    private static ResultSet ReadResults()
    {
        ModuleInfo[] modules = ReadCsv<ModuleInfo>("result_modules.csv");
        NamespaceInfo[] nsInfos = ReadCsv<NamespaceInfo>("result_namespaces.csv");
        ResultInfo[] results = ReadCsv<ResultInfo>("results.csv");

        Dictionary<int, ModuleInfo> moduleDict = modules.ToDictionary(m => m.Id);

        // Make sure modules have a default namespace
        foreach (ModuleInfo module in modules)
        {
            if (string.IsNullOrWhiteSpace(module.Namespace))
            {
                module.Namespace = module.Name;
            }
        }

        // Populate result module name and namespace fields if needed
        foreach (ResultInfo result in results)
        {
            result.ModuleName = moduleDict[result.ModuleId].Name;

            if (string.IsNullOrWhiteSpace(result.Namespace))
            {
                result.Namespace = moduleDict[result.ModuleId].Namespace;
            }
        }

        // Group results by namespace
        foreach (NamespaceInfo nsInfo in nsInfos)
        {
            // Sort DescriptionEnd by descending so any abstract ranges are put before an actual result at that description value
            nsInfo.Results = results.Where(x => x.Namespace == nsInfo.Name).OrderBy(x => x.DescriptionStart)
                .ThenByDescending(x => x.DescriptionEnd).ToArray();

            if (nsInfo.Results.Length == 0)
                continue;

            // Set the namespace's result module name
            string moduleName = nsInfo.Results.First().ModuleName;
            if (nsInfo.Results.Any(x => x.ModuleName != moduleName))
            {
                throw new InvalidDataException(
                    $"Error with namespace \"{nsInfo.Name}\": All results in a namespace must be from the same module.");
            }

            nsInfo.ModuleId = nsInfo.Results.First().ModuleId;
            nsInfo.ModuleName = moduleName;
        }

        // Group results by module
        foreach (ModuleInfo module in modules)
        {
            // Sort DescriptionEnd by descending so any abstract ranges are put before an actual result at that description value
            module.Results = results.Where(x => x.ModuleId == module.Id).OrderBy(x => x.DescriptionStart)
                .ThenByDescending(x => x.DescriptionEnd).ToArray();
        }

        return new ResultSet
        {
            Modules = modules.ToList(),
            Namespaces = nsInfos.ToList(),
            Results = results.ToList()
        };
    }

    private static void SetEmptyResultValues(ResultSet resultSet)
    {
        foreach (ResultInfo result in resultSet.Results)
        {
            result.FullName = $"Result{result.ModuleName}{result.Name}";

            if (string.IsNullOrWhiteSpace(result.Name))
            {
                if (result.IsRange)
                {
                    result.Name += $"Range{result.DescriptionStart}To{result.DescriptionEnd}";
                }
                else
                {
                    result.Name = $"Result{result.DescriptionStart}";
                    result.DescriptionEnd = result.DescriptionStart;
                }
            }
        }
    }

    private static void ValidateResults(ResultSet resultSet)
    {
        // Make sure all the result values are in range
        foreach (ResultInfo result in resultSet.Results)
        {
            // Logic should match Result.Base.ctor
            Assert(1 <= result.ModuleId && result.ModuleId < 512, "Invalid Module");
            Assert(0 <= result.DescriptionStart && result.DescriptionStart < 8192, "Invalid Description Start");
            Assert(0 <= result.DescriptionEnd && result.DescriptionEnd < 8192, "Invalid Description End");
            Assert(result.DescriptionStart <= result.DescriptionEnd, "descriptionStart must be <= descriptionEnd");

            // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
            void Assert(bool condition, string message)
            {
                if (!condition)
                    throw new InvalidDataException($"Result {result.ModuleId}-{result.DescriptionStart}: {message}");
            }
        }

        // Make sure all the result namespaces match a known namespace
        string[] namespaceNames = resultSet.Namespaces.Select(x => x.Name).ToArray();

        foreach (string nsName in resultSet.Results.Select(x => x.Namespace).Distinct())
        {
            if (!namespaceNames.Contains(nsName))
            {
                throw new InvalidDataException($"Invalid result namespace \"{nsName}\"");
            }
        }
    }

    private static void CheckForDuplicates(ResultSet resultSet)
    {
        var moduleIdSet = new HashSet<int>();
        var moduleNameSet = new HashSet<string>();

        foreach (ModuleInfo module in resultSet.Modules)
        {
            if (!moduleIdSet.Add(module.Id))
            {
                throw new InvalidDataException($"Duplicate result module index {module.Id}.");
            }

            if (!moduleNameSet.Add(module.Name))
            {
                throw new InvalidDataException($"Duplicate result module name {module.Name}.");
            }

            var descriptionSet = new HashSet<int>();
            var descriptionSetAbstract = new HashSet<int>();

            foreach (ResultInfo result in module.Results)
            {
                if (result.IsAbstract)
                {
                    if (!descriptionSetAbstract.Add(result.DescriptionStart))
                    {
                        throw new InvalidDataException(
                            $"Duplicate abstract result {result.ModuleId}-{result.DescriptionStart}-{result.DescriptionEnd}.");
                    }
                }
                else
                {
                    if (!descriptionSet.Add(result.DescriptionStart))
                    {
                        throw new InvalidDataException(
                            $"Duplicate result {result.ModuleId}-{result.DescriptionStart}-{result.DescriptionEnd}.");
                    }
                }
            }
        }
    }

    private static void ValidateHierarchy(ResultSet resultSet)
    {
        foreach (ModuleInfo module in resultSet.Modules)
        {
            var hierarchy = new Stack<ResultInfo>();

            foreach (ResultInfo result in module.Results)
            {
                while (hierarchy.Count > 0 && hierarchy.Peek().DescriptionEnd < result.DescriptionStart)
                {
                    hierarchy.Pop();
                }

                if (result.IsRange)
                {
                    if (hierarchy.Count > 0 && result.DescriptionEnd > hierarchy.Peek().DescriptionEnd)
                    {
                        throw new InvalidDataException($"Result {result.ModuleId}-{result.DescriptionStart} is not nested properly.");
                    }

                    hierarchy.Push(result);
                }
            }
        }
    }

    private static void CheckIfAggressiveInliningNeeded(ResultSet resultSet)
    {
        foreach (NamespaceInfo ns in resultSet.Namespaces)
        {
            ns.NeedsAggressiveInlining = ns.Results.Any(x => EstimateCilSize(x) > InlineThreshold);
        }
    }

    private static string PrintModule(NamespaceInfo ns)
    {
        var sb = new IndentingStringBuilder();

        sb.AppendLine(GetHeader());
        sb.AppendLine();

        if (ns.NeedsAggressiveInlining)
        {
            sb.AppendLine("using System.Runtime.CompilerServices;");
            sb.AppendLine();
        }

        sb.AppendLine($"namespace LibHac.{ns.Name};");
        sb.AppendLine();

        sb.AppendLine($"public static class Result{ns.ClassName}");
        sb.AppendLineAndIncrease("{");

        sb.AppendLine($"public const int Module{ns.ModuleName} = {ns.ModuleId};");
        sb.AppendLine();

        var hierarchy = new Stack<ResultInfo>();
        bool justIndented = false;

        foreach (ResultInfo result in ns.Results)
        {
            while (hierarchy.Count > 0 && hierarchy.Peek().DescriptionEnd < result.DescriptionStart)
            {
                hierarchy.Pop();
                sb.DecreaseLevel();
                sb.AppendSpacerLine();
            }

            if (!justIndented && result.IsRange)
            {
                sb.AppendSpacerLine();
            }

            PrintResult(sb, ns.ModuleName, result);

            if (result.IsRange)
            {
                hierarchy.Push(result);
                sb.IncreaseLevel();
            }

            justIndented = result.IsRange;
        }

        while (hierarchy.Count > 0)
        {
            hierarchy.Pop();
            sb.DecreaseLevel();
        }

        sb.DecreaseAndAppend("}");

        return sb.ToString();
    }

    private static void PrintResult(IndentingStringBuilder sb, string moduleName, ResultInfo result)
    {
        string descriptionArgs;

        if (result.IsRange)
        {
            descriptionArgs = $"{result.DescriptionStart}, {result.DescriptionEnd}";
        }
        else
        {
            descriptionArgs = $"{result.DescriptionStart}";
        }

        sb.AppendLine(GetXmlDoc(result));

        string type = result.IsAbstract ? "Result.Base.Abstract" : "Result.Base";

        string resultCtor = $"new {type}(Module{moduleName}, {descriptionArgs});";
        sb.Append($"public static {type} {result.Name} ");

        if (EstimateCilSize(result) > InlineThreshold)
        {
            sb.AppendLine($"{{ [MethodImpl(MethodImplOptions.AggressiveInlining)] get => {resultCtor} }}");
        }
        else
        {
            sb.AppendLine($"=> {resultCtor}");
        }
    }

    private static string GetXmlDoc(ResultInfo result)
    {
        string doc = "/// <summary>";

        if (!string.IsNullOrWhiteSpace(result.Summary))
        {
            doc += $"{result.Summary}<br/>";
        }

        doc += $"Error code: {result.ErrorCode}";

        if (result.IsRange)
        {
            doc += $"; Range: {result.DescriptionStart}-{result.DescriptionEnd}";
        }

        if (!result.IsAbstract)
        {
            doc += $"; Inner value: 0x{result.InnerValue:x}";
        }

        doc += "</summary>";

        return doc;
    }

    private static byte[] BuildArchive(ResultSet resultSet)
    {
        var builder = new ResultArchiveBuilder();

        foreach (NamespaceInfo module in resultSet.Namespaces.OrderBy(x => x.ModuleId))
        {
            foreach (ResultInfo result in module.Results.OrderBy(x => x.DescriptionStart))
            {
                builder.Add(result);
            }
        }

        return builder.Build();
    }

    private static string PrintArchive(ReadOnlySpan<byte> data)
    {
        var sb = new IndentingStringBuilder();

        sb.AppendLine(GetHeader());
        sb.AppendLine();

        sb.AppendLine("using System;");
        sb.AppendLine();

        sb.AppendLine("namespace LibHac.Common;");
        sb.AppendLine();

        sb.AppendLine("internal partial class ResultNameResolver");
        sb.AppendLineAndIncrease("{");

        sb.AppendLine("private static ReadOnlySpan<byte> ArchiveData =>");
        sb.AppendLineAndIncrease("[");

        for (int i = 0; i < data.Length; i++)
        {
            if (i % 16 != 0) sb.Append(" ");
            sb.Append($"0x{data[i]:x2}");

            if (i != data.Length - 1)
            {
                sb.Append(",");
                if (i % 16 == 15) sb.AppendLine();
            }
        }

        sb.AppendLine();
        sb.DecreaseAndAppendLine("];");
        sb.DecreaseAndAppend("}");

        return sb.ToString();
    }

    private static T[] ReadCsv<T>(string name)
    {
        var configuration = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            AllowComments = true,
            DetectColumnCountChanges = true
        };

        using (var csv = new CsvReader(new StreamReader(GetResource(name)), configuration))
        {
            csv.Context.RegisterClassMap<ModuleMap>();
            csv.Context.RegisterClassMap<NamespaceMap>();
            csv.Context.RegisterClassMap<ResultMap>();

            return csv.GetRecords<T>().ToArray();
        }
    }

    private static int EstimateCilSize(ResultInfo result)
    {
        int size = 0;

        size += GetLoadSize(result.ModuleId);
        size += GetLoadSize(result.DescriptionStart);

        if (result.IsRange)
            size += GetLoadSize(result.DescriptionEnd);

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

    public static string PrintEnum(ResultSet resultSet)
    {
        var sb = new StringBuilder();
        int[] printUnknownResultsForModules = [2];
        int[] skipModules = [428];

        foreach (ModuleInfo module in resultSet.Modules.Where(x => !skipModules.Contains(x.Id)))
        {
            bool printAllResults = printUnknownResultsForModules.Contains(module.Id);
            int prevResult = 1;

            foreach (ResultInfo result in module.Results)
            {
                if (printAllResults && result.DescriptionStart > prevResult + 1)
                {
                    for (int i = prevResult + 1; i < result.DescriptionStart; i++)
                    {
                        int innerValue = 2 & 0x1ff | ((i & 0x7ffff) << 9);
                        string unknownResultLine = $"Result_{result.ModuleId}_{i} = {innerValue},";
                        sb.AppendLine(unknownResultLine);
                    }
                }

                string name = string.IsNullOrWhiteSpace(result.Name) ? string.Empty : $"_{result.Name}";
                string line = $"Result_{result.ModuleId}_{result.DescriptionStart}{name} = {result.InnerValue},";

                sb.AppendLine(line);
                prevResult = result.DescriptionStart;
            }

            if (printAllResults)
            {
                for (int i = prevResult + 1; i < 8192; i++)
                {
                    int innerValue = 2 & 0x1ff | ((i & 0x7ffff) << 9);
                    string unknownResultLine = $"Result_{module.Id}_{i} = {innerValue},";
                    sb.AppendLine(unknownResultLine);
                }
            }
        }

        return sb.ToString();
    }
}

public class ResultArchiveBuilder
{
    private List<ResultInfo> Results = new List<ResultInfo>();

    public void Add(ResultInfo result)
    {
        Results.Add(result);
    }

    public byte[] Build()
    {
        int tableOffset = CalculateNameTableOffset();
        byte[] archive = new byte[tableOffset + CalculateNameTableSize()];

        ref HeaderStruct header = ref Unsafe.As<byte, HeaderStruct>(ref archive[0]);
        Span<Element> elements = MemoryMarshal.Cast<byte, Element>(
            archive.AsSpan(Unsafe.SizeOf<HeaderStruct>(), Results.Count * Unsafe.SizeOf<Element>()));
        Span<byte> nameTable = archive.AsSpan(tableOffset);

        header.ElementCount = Results.Count;
        header.NameTableOffset = tableOffset;

        int curNameOffset = 0;

        for (int i = 0; i < Results.Count; i++)
        {
            ResultInfo result = Results[i];
            ref Element element = ref elements[i];

            element.NameOffset = curNameOffset;
            element.Module = (short)result.ModuleId;
            element.DescriptionStart = (short)result.DescriptionStart;
            element.DescriptionEnd = (short)result.DescriptionEnd;
            element.IsAbstract = result.IsAbstract;

            Span<byte> utf8Name = Encoding.UTF8.GetBytes(result.FullName);
            utf8Name.CopyTo(nameTable.Slice(curNameOffset));
            nameTable[curNameOffset + utf8Name.Length] = 0;

            curNameOffset += utf8Name.Length + 1;
        }

        return archive;
    }

    private int CalculateNameTableOffset()
    {
        return Unsafe.SizeOf<HeaderStruct>() + Unsafe.SizeOf<Element>() * Results.Count;
    }

    private int CalculateNameTableSize()
    {
        int size = 0;
        Encoding encoding = Encoding.UTF8;

        foreach (ResultInfo result in Results)
        {
            size += encoding.GetByteCount(result.FullName) + 1;
        }

        return size;
    }

    // ReSharper disable NotAccessedField.Local
    private struct HeaderStruct
    {
        public int ElementCount;
        public int NameTableOffset;
    }

    private struct Element
    {
        public int NameOffset;
        public short Module;
        public short DescriptionStart;
        public short DescriptionEnd;
        public bool IsAbstract;
    }
    // ReSharper restore NotAccessedField.Local
}

public class ModuleInfo
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Namespace { get; set; }

    public ResultInfo[] Results { get; set; }
}

[DebuggerDisplay("{" + nameof(ClassName) + ",nq}")]
public class NamespaceInfo
{
    public string Name { get; set; }
    public string ClassName { get; set; }
    public int ModuleId { get; set; }
    public string ModuleName { get; set; }
    public string Path { get; set; }

    public bool NeedsAggressiveInlining { get; set; }
    public ResultInfo[] Results { get; set; }
}

[DebuggerDisplay("{" + nameof(Name) + ",nq}")]
public class ResultInfo
{
    public int ModuleId { get; set; }
    public int DescriptionStart { get; set; }
    public int DescriptionEnd { get; set; }
    public ResultInfoFlags Flags { get; set; }
    public string Name { get; set; }
    public string ModuleName { get; set; }
    public string Namespace { get; set; }
    public string FullName { get; set; }
    public string Summary { get; set; }

    public bool IsRange => DescriptionStart != DescriptionEnd;
    public string ErrorCode => $"{2000 + ModuleId:d4}-{DescriptionStart:d4}";
    public int InnerValue => ModuleId & 0x1ff | ((DescriptionStart & 0x7ffff) << 9);
    public bool IsAbstract => Flags.HasFlag(ResultInfoFlags.Abstract);
}

public class ResultSet
{
    public List<ModuleInfo> Modules { get; set; }
    public List<NamespaceInfo> Namespaces { get; set; }
    public List<ResultInfo> Results { get; set; }
}

[Flags]
public enum ResultInfoFlags
{
    None = 0,
    Abstract = 1 << 0
}

public sealed class ModuleMap : ClassMap<ModuleInfo>
{
    public ModuleMap()
    {
        Map(m => m.Id);
        Map(m => m.Name);
        Map(m => m.Namespace).Convert(row =>
        {
            string field = row.Row.GetField("Default Namespace");
            if (string.IsNullOrWhiteSpace(field))
                field = row.Row.GetField("Name");

            return field;
        });
    }
}

public sealed class NamespaceMap : ClassMap<NamespaceInfo>
{
    public NamespaceMap()
    {
        Map(m => m.Name).Name("Namespace");
        Map(m => m.Path);
        Map(m => m.ClassName).Convert(row =>
        {
            string field = row.Row.GetField("Class Name");
            if (string.IsNullOrWhiteSpace(field))
                field = row.Row.GetField("Namespace");

            return field;
        });
    }
}

public sealed class ResultMap : ClassMap<ResultInfo>
{
    public ResultMap()
    {
        Map(m => m.ModuleId).Name("Module");
        Map(m => m.Namespace);
        Map(m => m.Name);
        Map(m => m.Summary);

        Map(m => m.DescriptionStart);
        Map(m => m.DescriptionEnd).Convert(row =>
        {
            string field = row.Row.GetField("DescriptionEnd");
            if (string.IsNullOrWhiteSpace(field))
                field = row.Row.GetField("DescriptionStart");

            return int.Parse(field);
        });

        Map(m => m.Flags).Convert(row =>
        {
            string field = row.Row.GetField("Flags");
            var flags = ResultInfoFlags.None;

            foreach (char c in field)
            {
                switch (c)
                {
                    case 'a':
                        flags |= ResultInfoFlags.Abstract;
                        break;

                    default:
                        throw new InvalidDataException($"Invalid Result flag '{c}'");
                }
            }

            return flags;
        });
    }
}