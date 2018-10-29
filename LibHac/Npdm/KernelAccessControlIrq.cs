namespace LibHac.Npdm
{
    public struct KernelAccessControlIrq
    {
        public uint Irq0 { get; private set; }
        public uint Irq1 { get; private set; }

        public KernelAccessControlIrq(uint irq0, uint irq1)
        {
            this.Irq0 = irq0;
            this.Irq1 = irq1;
        }
    }
}