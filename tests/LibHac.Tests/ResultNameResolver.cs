using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace LibHac.Tests
{
    internal class ResultNameResolver : Result.IResultNameResolver
    {
        private Lazy<Dictionary<Result, string>> ResultNames { get; } = new Lazy<Dictionary<Result, string>>(GetResultNames);

        public bool TryResolveName(Result result, out string name)
        {
            return ResultNames.Value.TryGetValue(result, out name);
        }

        private static Dictionary<Result, string> GetResultNames()
        {
            var dict = new Dictionary<Result, string>();

            Assembly assembly = typeof(Result).Assembly;

            foreach (TypeInfo type in assembly.DefinedTypes.Where(x => x.Name.Contains("Result")))
                foreach (PropertyInfo property in type.DeclaredProperties
                    .Where(x => x.PropertyType == typeof(Result.Base) && x.GetMethod.IsStatic && x.SetMethod == null))
                {
                    Result value = ((Result.Base)property.GetValue(null, null)).Value;
                    string name = $"{type.Name}{property.Name}";

                    dict[value] = name;
                }

            return dict;
        }
    }
}
