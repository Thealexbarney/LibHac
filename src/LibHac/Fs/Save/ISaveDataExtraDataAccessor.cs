namespace LibHac.Fs.Save
{
    public interface ISaveDataExtraDataAccessor
    {
        Result Write(ExtraData data);
        Result Commit();
        Result Read(out ExtraData data);
    }
}
