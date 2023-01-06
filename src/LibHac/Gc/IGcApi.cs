using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Gc.Writer;

namespace LibHac.Gc;

public interface IGcApi
{
    IGcWriterApi Writer { get; }
    void InsertGameCard(in SharedRef<IStorage> storage);
    void RemoveGameCard();
    void PresetInternalKeys(ReadOnlySpan<byte> gameCardKey, ReadOnlySpan<byte> gameCardCertificate);
    void Initialize(Memory<byte> workBuffer, ulong deviceBufferAddress);
    void FinalizeGc();
    void PowerOffGameCard();
    void RegisterDeviceVirtualAddress(Memory<byte> buffer, ulong deviceBufferAddress);
    void UnregisterDeviceVirtualAddress(Memory<byte> buffer, ulong deviceBufferAddress);
    Result GetInitializationResult();
    Result Activate();
    void Deactivate();
    Result SetCardToSecureMode();
    Result Read(Span<byte> destination, uint pageAddress, uint pageCount);
    void PutToSleep();
    void Awaken();
    bool IsCardInserted();
    bool IsCardActivationValid();
    Result GetCardStatus(out GameCardStatus outStatus);
    Result GetCardDeviceId(Span<byte> destBuffer);
    Result GetCardDeviceCertificate(Span<byte> destBuffer);
    Result ChallengeCardExistence(Span<byte> responseBuffer, ReadOnlySpan<byte> challengeSeedBuffer, ReadOnlySpan<byte> challengeValueBuffer);
    Result GetCardImageHash(Span<byte> destBuffer);
    Result GetGameCardIdSet(out GameCardIdSet outGcIdSet);
    void RegisterDetectionEventCallback(Action<object> function, object args);
    void UnregisterDetectionEventCallback();
    Result GetCardHeader(Span<byte> destBuffer);
    Result GetErrorInfo(out GameCardErrorReportInfo outErrorReportInfo);
}