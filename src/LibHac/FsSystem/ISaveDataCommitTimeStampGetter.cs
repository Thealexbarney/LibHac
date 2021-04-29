namespace LibHac.FsSystem
{
    public interface ISaveDataCommitTimeStampGetter
    {
        Result Get(out long timeStamp);
    }
}