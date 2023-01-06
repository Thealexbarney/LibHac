using System;
using LibHac.Gc.Impl;

namespace LibHac.Gc.Writer;

public interface IGcWriterApi
{
    void ChangeMode(AsicMode mode);
    Result ActivateForWriter();
    Result EraseAndWriteParameter(MemorySize size, uint romAreaStartPageIndex);
    Result Write(ReadOnlySpan<byte> source, uint pageIndex, uint pageCount);
    Result GetCardAvailableRawSize(out long outSize);
    void SetVerifyEnableFlag(bool isEnabled);
    void SetUserAsicFirmwareBuffer(ReadOnlySpan<byte> firmwareBuffer);
    Result GetRmaInformation(out RmaInformation outRmaInformation);
    Result WriteDevCardParam(in DevCardParameter devCardParam);
    Result ReadDevCardParam(out DevCardParameter outDevCardParam);
    Result ForceErase();
}