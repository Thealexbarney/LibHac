namespace LibHac.Npdm
{
    public struct KernelAccessControlMmio
    {
        public ulong Address  { get; private set; }
        public ulong Size     { get; private set; }
        public bool  IsRo     { get; private set; }
        public bool  IsNormal { get; private set; }

        public KernelAccessControlMmio(
            ulong address,
            ulong size,
            bool  isro,
            bool  isnormal)
        {
            this.Address  = address;
            this.Size     = size;
            this.IsRo     = isro;
            this.IsNormal = isnormal;
        }
    }
}