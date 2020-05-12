using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LibHac;

namespace hactoolnet
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
                    .Where(x => x.PropertyType == typeof(Result.Base) && x.GetMethod?.IsStatic == true && x.SetMethod == null))
                {
                    object value = property.GetValue(null, null);
                    if (value is null) continue;

                    Result resultValue = ((Result.Base)value).Value;
                    string name = $"{type.Name}{property.Name}";

                    dict[resultValue] = name;
                }

            return dict;
        }
    }
}
