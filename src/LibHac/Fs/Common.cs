using System.Runtime.InteropServices;

namespace LibHac.Fs;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct Int64
{
    private long _value;

    public void Set(long value)
    {
        _value = value;
    }

    public readonly long Get()
    {
        return _value;
    }

    public static implicit operator long(in Int64 value) => value.Get();
}