using System;

namespace LibHac.FsSystem;

// Todo: Actually implement both of these structs
public struct ScopedThreadPriorityChanger : IDisposable
{
    public enum Mode
    {
        Absolute,
        Relative
    }

    public ScopedThreadPriorityChanger(int priority, Mode mode)
    {
        // Change the current thread priority
    }

    public void Dispose()
    {
        // Change thread priority back
    }
}

public struct ScopedThreadPriorityChangerByAccessPriority : IDisposable
{
    public enum AccessMode
    {
        Read,
        Write
    }

    private ScopedThreadPriorityChanger _scopedChanger;

    public ScopedThreadPriorityChangerByAccessPriority(AccessMode mode)
    {
        _scopedChanger = new ScopedThreadPriorityChanger(GetThreadPriorityByAccessPriority(mode),
            ScopedThreadPriorityChanger.Mode.Absolute);
    }

    public void Dispose()
    {
        _scopedChanger.Dispose();
    }

    private static int GetThreadPriorityByAccessPriority(AccessMode mode)
    {
        return 0;
    }
}