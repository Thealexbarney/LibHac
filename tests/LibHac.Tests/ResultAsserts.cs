using LibHac;
using Xunit.Sdk;

// ReSharper disable once CheckNamespace
namespace Xunit
{
    public partial class Assert
    {
        public static void Success(Result result)
        {
            Equal(LibHac.Result.Success, result);
        }

        public static void Failure(Result result)
        {
            NotEqual(LibHac.Result.Success, result);
        }

        public static void Result(Result.Base expected, Result actual)
        {
            if (!expected.Includes(actual))
                throw new EqualException(expected.Value, actual);
        }
    }
}
