using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using LibHac;
using LibHac.Fs;

namespace hactoolnet
{
    public static class ResultLogFunctions
    {
        private static Dictionary<Result, string> ResultNames { get; } = GetResultNames();

        public static TextWriter LogWriter { get; set; }

        public static void LogResult(Result result)
        {
            if (LogWriter == null) return;

            var st = new StackTrace(2, true);

            if (st.FrameCount > 1)
            {
                MethodBase method = st.GetFrame(0).GetMethod();

                // This result from these functions is usually noise because they
                // are frequently used to detect if a file exists
                if (ResultFs.PathNotFound.Includes(result) &&
                    typeof(IFileSystem).IsAssignableFrom(method.DeclaringType) &&
                    method.Name.StartsWith(nameof(IFileSystem.GetEntryType)) ||
                    method.Name.StartsWith(nameof(IAttributeFileSystem.GetFileAttributes)))
                {
                    return;
                }

                string methodName = $"{method.DeclaringType?.FullName}.{method.Name}";

                LogWriter.WriteLine($"{result.ToStringWithName()} returned by {methodName}");
                LogWriter.WriteLine(st);
            }
        }

        public static void LogConvertedResult(Result result, Result originalResult)
        {
            if (LogWriter == null) return;

            var st = new StackTrace(2, false);

            if (st.FrameCount > 1)
            {
                MethodBase method = st.GetFrame(0).GetMethod();

                string methodName = $"{method.DeclaringType?.FullName}.{method.Name}";

                LogWriter.WriteLine($"{originalResult.ToStringWithName()} was converted to {result.ToStringWithName()} by {methodName}");
            }
        }

        public static Dictionary<Result, string> GetResultNames()
        {
            var dict = new Dictionary<Result, string>();

            Assembly assembly = typeof(Result).Assembly;

            foreach (Type type in assembly.GetTypes().Where(x => x.Name.Contains("Result")))
            {
                foreach (PropertyInfo property in type.GetProperties()
                    .Where(x => x.PropertyType == typeof(Result) && x.GetMethod.IsStatic && x.SetMethod == null))
                {
                    var value = (Result)property.GetValue(null, null);
                    string name = $"{type.Name}{property.Name}";

                    dict.Add(value, name);
                }
            }

            return dict;
        }

        public static bool TryGetResultName(Result result, out string name)
        {
            return ResultNames.TryGetValue(result, out name);
        }
    }
}
