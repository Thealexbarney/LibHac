namespace LibHac.Npdm
{
    public struct KernelAccessControlIrq
    {
        public uint Irq0 { get; }
        public uint Irq1 { get; }

        public KernelAccessControlIrq(uint irq0, uint irq1)
        {
            Irq0 = irq0;
            Irq1 = irq1;
        }
    }
}