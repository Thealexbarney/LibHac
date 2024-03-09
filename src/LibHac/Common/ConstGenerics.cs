namespace LibHac.Common;

public interface IConstant<T> where T : struct
{
    static abstract T Value { get; }
}