using static Vanara.PInvoke.Shell32;

namespace LibHac.FsSystem;

public static class SafeNativeMethods
{
    public static IShellItem CreateShellItem(string path)
    {
        if (!Vanara.PInvoke.ShlwApi.PathFileExists(path))
            return null;

        return ShellUtil.GetShellItemForPath(path);
    }
}