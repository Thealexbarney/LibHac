using LibHac.Fs;

namespace LibHac.FsService
{
    public interface ISaveDataIndexer
    {
        Result Get(out SaveDataIndexerValue value, ref SaveDataAttribute key);
    }
}