namespace LibHac.FsSystem;

/// <summary>
/// Gets Unix timestamps used to update a save data's commit time. 
/// </summary>
/// <remarks>Based on FS 13.1.0 (nnSdk 13.4.0)</remarks>
public interface ISaveDataCommitTimeStampGetter
{
    Result Get(out long timeStamp);
}