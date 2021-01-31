using System;
using LibHac.Os;
using LibHac.Svc;

namespace LibHac.Sf
{
    public class NativeHandle : IDisposable
    {
        private OsState Os { get; }
        public Handle Handle { get; private set; }
        public bool IsManaged { get; private set; }

        public NativeHandle(OsState os, Handle handle)
        {
            Os = os;
            Handle = handle;
        }

        public NativeHandle(OsState os, Handle handle, bool isManaged)
        {
            Handle = handle;
            IsManaged = isManaged;
        }

        public void Dispose()
        {
            if (IsManaged)
                Os.CloseNativeHandle(Handle.Object);
        }
    }
}
