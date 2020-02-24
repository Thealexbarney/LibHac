using LibHac.Fs;

namespace LibHac.FsService
{
    public class EmulatedDeviceOperator : IDeviceOperator
    {
        private EmulatedGameCard GameCard { get; set; }
        private EmulatedSdCard SdCard { get; set; }

        public EmulatedDeviceOperator(EmulatedGameCard gameCard, EmulatedSdCard sdCard)
        {
            GameCard = gameCard;
            SdCard = sdCard;
        }

        public Result IsSdCardInserted(out bool isInserted)
        {
            isInserted = SdCard.IsSdCardInserted();
            return Result.Success;
        }

        public Result IsGameCardInserted(out bool isInserted)
        {
            isInserted = GameCard.IsGameCardInserted();
            return Result.Success;
        }

        public Result GetGameCardHandle(out GameCardHandle handle)
        {
            if (!GameCard.IsGameCardInserted())
            {
                handle = default;
                return ResultFs.GameCardNotInsertedOnGetHandle.Log();
            }

            handle = GameCard.GetGameCardHandle();
            return Result.Success;
        }
    }
}
