using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Shim;
using LibHac.FsSrv.Impl;
using LibHac.Ncm;
using Xunit;
using ContentType = LibHac.Fs.ContentType;

namespace LibHac.Tests.FsSrv
{
    public class AccessControlTests
    {
        [Fact]
        public void OpenFileSystemWithNoPermissions_ReturnsPermissionDenied()
        {
            Horizon hos = HorizonFactory.CreateBasicHorizon();

            HorizonClient regClient = hos.CreatePrivilegedHorizonClient();
            HorizonClient client = hos.CreateHorizonClient();

            var dataHeader = new AccessControlDataHeader();
            var descriptor = new AccessControlDescriptor();

            descriptor.Version = 123;
            dataHeader.Version = 123;

            descriptor.AccessFlags = (ulong)AccessControlBits.Bits.None;
            dataHeader.AccessFlags = (ulong)AccessControlBits.Bits.None;

            Assert.Success(regClient.Fs.RegisterProgram(client.ProcessId.Value, new ProgramId(123),
                StorageId.BuiltInUser, SpanHelpers.AsReadOnlyByteSpan(in dataHeader),
                SpanHelpers.AsReadOnlyByteSpan(in descriptor)));

            Result rc = client.Fs.MountContent("test".ToU8Span(), "@System:/fake.nca".ToU8Span(), ContentType.Control);
            Assert.Result(ResultFs.PermissionDenied, rc);
        }

        [Fact]
        public void OpenFileSystemWithPermissions_ReturnsInvalidNcaMountPoint()
        {
            Horizon hos = HorizonFactory.CreateBasicHorizon();

            HorizonClient regClient = hos.CreatePrivilegedHorizonClient();
            HorizonClient client = hos.CreateHorizonClient();

            var dataHeader = new AccessControlDataHeader();
            var descriptor = new AccessControlDescriptor();

            descriptor.Version = 123;
            dataHeader.Version = 123;

            descriptor.AccessFlags = (ulong)AccessControlBits.Bits.ApplicationInfo;
            dataHeader.AccessFlags = (ulong)AccessControlBits.Bits.ApplicationInfo;

            Assert.Success(regClient.Fs.RegisterProgram(client.ProcessId.Value, new ProgramId(123),
                StorageId.BuiltInUser, SpanHelpers.AsReadOnlyByteSpan(in dataHeader),
                SpanHelpers.AsReadOnlyByteSpan(in descriptor)));

            // We should get InvalidNcaMountPoint because mounting NCAs from @System isn't allowed
            Result rc = client.Fs.MountContent("test".ToU8Span(), "@System:/fake.nca".ToU8Span(), ContentType.Control);
            Assert.Result(ResultFs.InvalidNcaMountPoint, rc);
        }
    }
}
