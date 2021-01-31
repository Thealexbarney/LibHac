namespace LibHac.Svc
{
    public readonly struct Handle
    {
        public readonly object Object;

        public Handle(object obj)
        {
            Object = obj;
        }
    }
}
