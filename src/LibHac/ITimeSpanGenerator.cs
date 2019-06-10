using System;

namespace LibHac
{
    public interface ITimeSpanGenerator
    {
        TimeSpan GetCurrent();
    }
}