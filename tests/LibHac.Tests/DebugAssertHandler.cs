﻿using System.Diagnostics;

namespace LibHac.Tests
{
    public class DebugAssertHandler : DefaultTraceListener
    {
        public override void Fail(string message, string detailMessage)
        {
            // throw new System.NotImplementedException(message + detailMessage);
        }
    }
}
