namespace LibHac
{
    public struct ResultRange
    {
        private Result Start { get; }
        private Result End { get; }

        public ResultRange(int module, int descriptionStart, int descriptionEnd)
        {
            Start = new Result(module, descriptionStart);
            End = new Result(module, descriptionEnd);
        }

        public bool Contains(Result result)
        {
            return result.Module == Start.Module &&
                   result.Description >= Start.Description &&
                   result.Description <= End.Description;
        }
    }
}
