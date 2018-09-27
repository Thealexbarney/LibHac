using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Net
{
    internal static class CliParser
    {
        private static readonly CliOption[] CliOptions =
        {
            new CliOption("keyset", 'k', 1, (o, a) => o.Keyfile = a[0]),
            new CliOption("titlekeys", 1, (o, a) => o.TitleKeyFile = a[0]),
            new CliOption("consolekeys", 1, (o, a) => o.ConsoleKeyFile = a[0]),
            new CliOption("title", 1, (o, a) => o.TitleId = ParseTitleId(a[0])),
            new CliOption("version", 1, (o, a) => o.Version = ParseVersion(a[0])),
            new CliOption("did", 1, (o, a) => o.DeviceId = ParseTitleId(a[0])),
            new CliOption("cert", 1, (o, a) => o.CertFile = a[0]),
            new CliOption("commoncert", 1, (o, a) => o.CommonCertFile = a[0]),
            new CliOption("token", 1, (o, a) => o.Token = a[0]),
            new CliOption("metadata", 0, (o, a) => o.GetMetadata = true)
        };

        public static Options Parse(string[] args)
        {
            var options = new Options();

            for (int i = 0; i < args.Length; i++)
            {
                string arg;

                if (args[i].Length == 2 && (args[i][0] == '-' || args[i][0] == '/'))
                {
                    arg = args[i][1].ToString().ToLower();
                }
                else if (args[i].Length > 2 && args[i].Substring(0, 2) == "--")
                {
                    arg = args[i].Substring(2).ToLower();
                }
                else
                {
                    PrintWithUsage($"Unable to parse option {args[i]}");
                    return null;
                }

                var option = CliOptions.FirstOrDefault(x => x.Long == arg || x.Short == arg);
                if (option == null)
                {
                    PrintWithUsage($"Unknown option {args[i]}");
                    return null;
                }

                if (i + option.ArgsNeeded >= args.Length)
                {
                    PrintWithUsage($"Need {option.ArgsNeeded} parameter{(option.ArgsNeeded == 1 ? "" : "s")} after {args[i]}");
                    return null;
                }

                var optionArgs = new string[option.ArgsNeeded];
                Array.Copy(args, i + 1, optionArgs, 0, option.ArgsNeeded);

                option.Assigner(options, optionArgs);
                i += option.ArgsNeeded;
            }


            return options;
        }

        private static ulong ParseTitleId(string input)
        {
            if (input.Length != 16)
            {
                PrintWithUsage("Title ID must be 16 hex characters long");
            }

            if (!ulong.TryParse(input, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var id))
            {
                PrintWithUsage("Could not parse title ID");
            }

            return id;
        }

        private static int ParseVersion(string input)
        {
            if (!int.TryParse(input, out var version))
            {
                PrintWithUsage("Could not parse version");
            }

            return version;
        }

        internal static void PrintWithUsage(string toPrint)
        {
            Console.WriteLine(toPrint);
            Console.WriteLine(GetUsage());
            // PrintUsage();
        }

        private static string GetUsage()
        {
            var sb = new StringBuilder();

            sb.AppendLine("Usage: Don't");

            return sb.ToString();
        }

        private class CliOption
        {
            public CliOption(string longName, char shortName, int argsNeeded, Action<Options, string[]> assigner)
            {
                Long = longName;
                Short = shortName.ToString();
                ArgsNeeded = argsNeeded;
                Assigner = assigner;
            }
            public CliOption(string longName, int argsNeeded, Action<Options, string[]> assigner)
            {
                Long = longName;
                ArgsNeeded = argsNeeded;
                Assigner = assigner;
            }

            public string Long { get; }
            public string Short { get; }
            public int ArgsNeeded { get; }
            public Action<Options, string[]> Assigner { get; }
        }
    }
}
