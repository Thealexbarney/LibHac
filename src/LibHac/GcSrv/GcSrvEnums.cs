namespace LibHac.GcSrv;

public enum GameCardManagerOperationIdValue
{
    Finalize = 1,
    GetHandle = 2,
    IsGameCardActivationValid = 3,
    GetInitializationResult = 4,
    GetGameCardErrorInfo = 5,
    GetGameCardErrorReportInfo = 6,
    SetVerifyEnableFlag = 7,
    GetGameCardAsicInfo = 8,
    GetGameCardDeviceIdForProdCard = 9,
    EraseAndWriteParamDirectly = 10,
    ReadParamDirectly = 11,
    WriteToGameCardDirectly = 12,
    ForceErase = 13,
    SimulateDetectionEventSignaled = 14
}

public enum GameCardOperationIdValue
{
    EraseGameCard = 1,
    GetGameCardIdSet = 2,
    GetGameCardDeviceId = 3,
    GetGameCardImageHash = 4,
    GetGameCardDeviceCertificate = 5,
    ChallengeCardExistence = 6,
    GetGameCardStatus = 7
}

public enum OpenGameCardAttribute : long
{
    ReadOnly = 0,
    SecureReadOnly = 1,
    WriteOnly = 2
}