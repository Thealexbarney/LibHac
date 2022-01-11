namespace LibHac.Common;

public class Box<T> where T : struct
{
    private T _value;

    public ref T Value => ref _value;

    public Box()
    {
        _value = new T();
    }
}