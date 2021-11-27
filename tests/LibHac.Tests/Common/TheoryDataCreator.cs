using Xunit;

namespace LibHac.Tests.Common;

public static class TheoryDataCreator
{
    public static TheoryData<int> CreateSequence(int start, int count)
    {
        var data = new TheoryData<int>();

        for (int i = 0; i < count; i++)
        {
            data.Add(start + i);
        }

        return data;
    }
}