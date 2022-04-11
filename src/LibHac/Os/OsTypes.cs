global using OsNativeHandle = System.Threading.WaitHandle;

namespace LibHac.Os;

public static class OsTypes
{
    public static OsNativeHandle InvalidNativeHandle => default;
}