using System;
using LibHac.Fs;
using LibHac.Gc.Impl;
using LibHac.Gc.Writer;

namespace LibHac.Gc;

public class GameCardDummy
{
    public GameCardWriter Writer => new GameCardWriter();

    public readonly struct GameCardWriter
    {
        public GameCardWriter()
        {

        }

        public void ChangeMode(AsicMode mode)
        {
            throw new NotImplementedException();
        }

        public Result ActivateForWriter()
        {
            throw new NotImplementedException();
        }

        public Result EraseAndWriteParameter(MemorySize size, uint romAreaStartPageIndex)
        {
            throw new NotImplementedException();
        }

        public Result Write(ReadOnlySpan<byte> source, uint pageIndex, uint pageCount)
        {
            throw new NotImplementedException();
        }

        public Result GetCardAvailableRawSize(out long outSize)
        {
            throw new NotImplementedException();
        }

        public void SetVerifyEnableFlag(bool isEnabled)
        {
            throw new NotImplementedException();
        }

        public void SetUserAsicFirmwareBuffer(ReadOnlySpan<byte> firmwareBuffer)
        {
            throw new NotImplementedException();
        }

        public Result GetRmaInformation(out RmaInformation outRmaInformation)
        {
            throw new NotImplementedException();
        }

        public Result WriteDevCardParam(in DevCardParameter devCardParam)
        {
            throw new NotImplementedException();
        }

        public Result ReadDevCardParam(out DevCardParameter outDevCardParam)
        {
            throw new NotImplementedException();
        }

        public Result ForceErase()
        {
            throw new NotImplementedException();
        }
    }

    public void PresetInternalKeys(ReadOnlySpan<byte> gameCardKey, ReadOnlySpan<byte> gameCardCertificate)
    {
        throw new NotImplementedException();
    }

    public void Initialize(Memory<byte> workBuffer, ulong deviceBufferAddress)
    {
        throw new NotImplementedException();
    }

    public void FinalizeGc()
    {
        throw new NotImplementedException();
    }

    public void PowerOffGameCard()
    {
        throw new NotImplementedException();
    }

    public void RegisterDeviceVirtualAddress(Memory<byte> buffer, ulong deviceBufferAddress)
    {
        throw new NotImplementedException();
    }

    public void UnregisterDeviceVirtualAddress(Memory<byte> buffer, ulong deviceBufferAddress)
    {
        throw new NotImplementedException();
    }

    public Result GetInitializationResult()
    {
        throw new NotImplementedException();
    }

    public Result Activate()
    {
        throw new NotImplementedException();
    }

    public void Deactivate()
    {
        throw new NotImplementedException();
    }

    public Result SetCardToSecureMode()
    {
        throw new NotImplementedException();
    }

    public Result Read(Span<byte> destination, uint pageIndex, uint pageCount)
    {
        throw new NotImplementedException();
    }

    public void PutToSleep()
    {
        throw new NotImplementedException();
    }

    public void Awaken()
    {
        throw new NotImplementedException();
    }

    public bool IsCardInserted()
    {
        throw new NotImplementedException();
    }

    public bool IsCardActivationValid()
    {
        throw new NotImplementedException();
    }

    public Result GetCardStatus(out GameCardStatus outStatus)
    {
        throw new NotImplementedException();
    }

    public Result GetCardDeviceId(Span<byte> destBuffer)
    {
        throw new NotImplementedException();
    }

    public Result GetCardDeviceCertificate(Span<byte> destBuffer)
    {
        throw new NotImplementedException();
    }

    public Result ChallengeCardExistence(Span<byte> responseBuffer, ReadOnlySpan<byte> challengeSeedBuffer,
        ReadOnlySpan<byte> challengeValueBuffer)
    {
        throw new NotImplementedException();
    }

    public Result GetCardImageHash(Span<byte> destBuffer)
    {
        throw new NotImplementedException();
    }

    public Result GetGameCardIdSet(out GameCardIdSet outGcIdSet)
    {
        throw new NotImplementedException();
    }

    public void RegisterDetectionEventCallback(Action<object> function, object args)
    {
        throw new NotImplementedException();
    }

    public void UnregisterDetectionEventCallback()
    {
        throw new NotImplementedException();
    }

    public Result GetCardHeader(Span<byte> destBuffer)
    {
        throw new NotImplementedException();
    }

    public Result GetErrorInfo(out GameCardErrorReportInfo outErrorReportInfo)
    {
        throw new NotImplementedException();
    }
}