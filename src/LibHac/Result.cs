namespace LibHac
{
    public struct Result
    {
        public readonly int Value;

        public Result(int value)
        {
            Value = value;
        }

        public Result(int module, int description)
        {
            Value = (description << 9) | module;
        }

        public int Description => (Value >> 9) & 0x1FFF;
        public int Module => Value & 0x1FF;

        public bool IsSuccess() => Value == 0;
        public bool IsFailure() => Value != 0;
    }

    public static class Results
    {
        public static readonly Result ResultSuccess = new Result(0);
    }
}
