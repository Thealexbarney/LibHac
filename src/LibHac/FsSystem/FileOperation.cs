using System;
using System.Runtime.InteropServices;
using static Vanara.PInvoke.Shell32;

namespace LibHac.FsSystem;

public class FileOperation : IDisposable
{
    private bool disposed;
    private IFileOperation fileOperation;

    public FileOperation()
    {
        fileOperation = (IFileOperation)Activator.CreateInstance(typeof(CFileOperations));
        fileOperation.SetOperationFlags(FILEOP_FLAGS.FOF_SILENT);
    }

    public void RenameItem(string source, string newName)
    {
        IShellItem sourceItem = SafeNativeMethods.CreateShellItem(source);

        fileOperation.RenameItem(sourceItem, newName, null);
    }

    public bool PerformOperations()
    {
        try
        {
            fileOperation.PerformOperations();
            return true;
        }
        catch (COMException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (!disposed)
        {
            disposed = true;
#pragma warning disable CA1416 // Validate platform compatibility
            Marshal.FinalReleaseComObject(fileOperation);
#pragma warning restore CA1416 // Validate platform compatibility
        }
    }
}