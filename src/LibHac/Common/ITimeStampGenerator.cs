using System;

namespace LibHac.Common
{
    public interface ITimeStampGenerator
    {
        DateTimeOffset Generate();
    }
}