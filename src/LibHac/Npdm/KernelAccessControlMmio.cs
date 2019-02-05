namespace LibHac.Npdm
{
    public struct KernelAccessControlMmio
    {
        public ulong Address { get; }
        public ulong Size { get; private set; }
        public bool IsRo { get; private set; }
        public bool IsNormal { get; private set; }

        public KernelAccessControlMmio(
            ulong address,
            ulong size,
            bool isro,
            bool isnormal)
        {
            Address = address;
            Size = size;
            IsRo = isro;
            IsNormal = isnormal;
        }
    }
}