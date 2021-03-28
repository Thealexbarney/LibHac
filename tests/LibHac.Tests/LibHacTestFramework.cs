using System.Diagnostics;
using System.Linq;
using LibHac.Diag;
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

        // Todo: Catch assertions in PathToolTestGenerator.cpp
        private static readonly string[] SkipAbortFunctions = { "Normalize" };

        private static void SetDebugHandler()
        {
            AssertionFailureHandler handler = (in AssertionInfo info) =>
            {
                if (SkipAbortFunctions.Contains(info.FunctionName))
                {
                    return AssertionFailureOperation.Continue;
                }

                return AssertionFailureOperation.Abort;
            };

            Assert.SetAssertionFailureHandler(handler);
            Trace.Listeners.Clear();
            Trace.Listeners.Add(new DebugAssertHandler());
        }

        private static void SetResultNames()
        {
            Result.SetNameResolver(new ResultNameResolver());
        }
    }
}
