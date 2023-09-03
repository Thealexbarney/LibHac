namespace LibHac.GcSrv;

/// <summary>
/// The operations that <see cref="GameCardManager"/> can perform on the game card ASIC and writable game cards.
/// </summary>
/// <remarks>Based on nnSdk 16.2.0 (FS 16.0.0)</remarks>
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

/// <summary>
/// The operations that <see cref="GameCardDeviceOperator"/> can perform on the inserted game card.
/// </summary>
/// <remarks>Based on nnSdk 16.2.0 (FS 16.0.0)</remarks>
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

/// <summary>
/// Specifies which mode the game card storage should be opened as.
/// </summary>
/// <remarks>Based on nnSdk 16.2.0 (FS 16.0.0)</remarks>
public enum OpenGameCardAttribute : long
{
    ReadOnly = 0,
    SecureReadOnly = 1,
    WriteOnly = 2
}