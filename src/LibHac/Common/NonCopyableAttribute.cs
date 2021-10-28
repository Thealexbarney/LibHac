using System;

namespace LibHac.Common
{
    [AttributeUsage(AttributeTargets.Struct)]
    public sealed class NonCopyableAttribute : System.Attribute { }

    [AttributeUsage(AttributeTargets.Struct)]
    public sealed class NonCopyableDisposableAttribute : System.Attribute { }
}
