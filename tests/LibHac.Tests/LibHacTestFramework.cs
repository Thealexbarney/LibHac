using System.Diagnostics;
using LibHac.Tests;
using Xunit.Abstractions;
using Xunit.Sdk;

[assembly: Xunit.TestFramework("LibHac.Tests." + nameof(LibHacTestFramework), "LibHac.Tests")]

namespace LibHac.Tests
{
    public class LibHacTestFramework : XunitTestFramework
    {
        public LibHacTestFramework(IMessageSink messageSink)
            : base(messageSink)
        {
            SetDebugHandler();
            SetResultNames();
        }

        private static void SetDebugHandler()
        {
            Trace.Listeners.Clear();
            Trace.Listeners.Add(new DebugAssertHandler());
        }

        private static void SetResultNames()
        {
            Result.SetNameResolver(new ResultNameResolver());
        }
    }
}
