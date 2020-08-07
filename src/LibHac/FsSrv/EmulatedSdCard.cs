namespace LibHac.FsSrv
{
    public class EmulatedSdCard
    {
        private bool IsInserted { get; set; }

        public bool IsSdCardInserted()
        {
            return IsInserted;
        }

        public void SetSdCardInsertionStatus(bool isInserted)
        {
            IsInserted = isInserted;
        }
    }
}
