namespace LibHac.Ncm
{
    public readonly struct SystemProgramId
    {
        public readonly ulong Value;

        public SystemProgramId(ulong value)
        {
            Value = value;
        }

        public static implicit operator ProgramId(SystemProgramId id) => new ProgramId(id.Value);

        public static bool IsSystemProgramId(ProgramId programId)
        {
            return Start <= programId && programId <= End;
        }

        public static bool IsSystemProgramId(SystemProgramId id) => true;

        public static SystemProgramId Start => new SystemProgramId(0x0100000000000000);

        public static SystemProgramId Fs => new SystemProgramId(0x0100000000000000);
        public static SystemProgramId Loader => new SystemProgramId(0x0100000000000001);
        public static SystemProgramId Ncm => new SystemProgramId(0x0100000000000002);
        public static SystemProgramId Pm => new SystemProgramId(0x0100000000000003);
        public static SystemProgramId Sm => new SystemProgramId(0x0100000000000004);
        public static SystemProgramId Boot => new SystemProgramId(0x0100000000000005);
        public static SystemProgramId Usb => new SystemProgramId(0x0100000000000006);
        public static SystemProgramId Tma => new SystemProgramId(0x0100000000000007);
        public static SystemProgramId Boot2 => new SystemProgramId(0x0100000000000008);
        public static SystemProgramId Settings => new SystemProgramId(0x0100000000000009);
        public static SystemProgramId Bus => new SystemProgramId(0x010000000000000A);
        public static SystemProgramId Bluetooth => new SystemProgramId(0x010000000000000B);
        public static SystemProgramId Bcat => new SystemProgramId(0x010000000000000C);
        public static SystemProgramId Dmnt => new SystemProgramId(0x010000000000000D);
        public static SystemProgramId Friends => new SystemProgramId(0x010000000000000E);
        public static SystemProgramId Nifm => new SystemProgramId(0x010000000000000F);
        public static SystemProgramId Ptm => new SystemProgramId(0x0100000000000010);
        public static SystemProgramId Shell => new SystemProgramId(0x0100000000000011);
        public static SystemProgramId BsdSockets => new SystemProgramId(0x0100000000000012);
        public static SystemProgramId Hid => new SystemProgramId(0x0100000000000013);
        public static SystemProgramId Audio => new SystemProgramId(0x0100000000000014);
        public static SystemProgramId LogManager => new SystemProgramId(0x0100000000000015);
        public static SystemProgramId Wlan => new SystemProgramId(0x0100000000000016);
        public static SystemProgramId Cs => new SystemProgramId(0x0100000000000017);
        public static SystemProgramId Ldn => new SystemProgramId(0x0100000000000018);
        public static SystemProgramId NvServices => new SystemProgramId(0x0100000000000019);
        public static SystemProgramId Pcv => new SystemProgramId(0x010000000000001A);
        public static SystemProgramId Ppc => new SystemProgramId(0x010000000000001B);
        public static SystemProgramId NvnFlinger => new SystemProgramId(0x010000000000001C);
        public static SystemProgramId Pcie => new SystemProgramId(0x010000000000001D);
        public static SystemProgramId Account => new SystemProgramId(0x010000000000001E);
        public static SystemProgramId Ns => new SystemProgramId(0x010000000000001F);
        public static SystemProgramId Nfc => new SystemProgramId(0x0100000000000020);
        public static SystemProgramId Psc => new SystemProgramId(0x0100000000000021);
        public static SystemProgramId CapSrv => new SystemProgramId(0x0100000000000022);
        public static SystemProgramId Am => new SystemProgramId(0x0100000000000023);
        public static SystemProgramId Ssl => new SystemProgramId(0x0100000000000024);
        public static SystemProgramId Nim => new SystemProgramId(0x0100000000000025);
        public static SystemProgramId Cec => new SystemProgramId(0x0100000000000026);
        public static SystemProgramId Tspm => new SystemProgramId(0x0100000000000027);
        public static SystemProgramId Spl => new SystemProgramId(0x0100000000000028);
        public static SystemProgramId Lbl => new SystemProgramId(0x0100000000000029);
        public static SystemProgramId Btm => new SystemProgramId(0x010000000000002A);
        public static SystemProgramId Erpt => new SystemProgramId(0x010000000000002B);
        public static SystemProgramId Time => new SystemProgramId(0x010000000000002C);
        public static SystemProgramId Vi => new SystemProgramId(0x010000000000002D);
        public static SystemProgramId Pctl => new SystemProgramId(0x010000000000002E);
        public static SystemProgramId Npns => new SystemProgramId(0x010000000000002F);
        public static SystemProgramId Eupld => new SystemProgramId(0x0100000000000030);
        public static SystemProgramId Arp => new SystemProgramId(0x0100000000000031);
        public static SystemProgramId Glue => new SystemProgramId(0x0100000000000031);
        public static SystemProgramId Eclct => new SystemProgramId(0x0100000000000032);
        public static SystemProgramId Es => new SystemProgramId(0x0100000000000033);
        public static SystemProgramId Fatal => new SystemProgramId(0x0100000000000034);
        public static SystemProgramId Grc => new SystemProgramId(0x0100000000000035);
        public static SystemProgramId Creport => new SystemProgramId(0x0100000000000036);
        public static SystemProgramId Ro => new SystemProgramId(0x0100000000000037);
        public static SystemProgramId Profiler => new SystemProgramId(0x0100000000000038);
        public static SystemProgramId Sdb => new SystemProgramId(0x0100000000000039);
        public static SystemProgramId Migration => new SystemProgramId(0x010000000000003A);
        public static SystemProgramId Jit => new SystemProgramId(0x010000000000003B);
        public static SystemProgramId JpegDec => new SystemProgramId(0x010000000000003C);
        public static SystemProgramId SafeMode => new SystemProgramId(0x010000000000003D);
        public static SystemProgramId Olsc => new SystemProgramId(0x010000000000003E);
        public static SystemProgramId Dt => new SystemProgramId(0x010000000000003F);
        public static SystemProgramId Nd => new SystemProgramId(0x0100000000000040);
        public static SystemProgramId Ngct => new SystemProgramId(0x0100000000000041);
        public static SystemProgramId Pgl => new SystemProgramId(0x0100000000000042);

        public static SystemProgramId End => new SystemProgramId(0x01000000000007FF);
    }

    public readonly struct SystemDataId
    {
        public readonly ulong Value;

        public SystemDataId(ulong value)
        {
            Value = value;
        }

        public static implicit operator DataId(SystemDataId id) => new DataId(id.Value);

        public static bool IsSystemDataId(DataId dataId)
        {
            return Start <= dataId && dataId <= End;
        }

        public static bool IsSystemDataId(SystemDataId id) => true;

        public static SystemDataId Start => new SystemDataId(0x0100000000000800);

        public static SystemDataId CertStore => new SystemDataId(0x0100000000000800);
        public static SystemDataId ErrorMessage => new SystemDataId(0x0100000000000801);
        public static SystemDataId MiiModel => new SystemDataId(0x0100000000000802);
        public static SystemDataId BrowserDll => new SystemDataId(0x0100000000000803);
        public static SystemDataId Help => new SystemDataId(0x0100000000000804);
        public static SystemDataId SharedFont => new SystemDataId(0x0100000000000805);
        public static SystemDataId NgWord => new SystemDataId(0x0100000000000806);
        public static SystemDataId SsidList => new SystemDataId(0x0100000000000807);
        public static SystemDataId Dictionary => new SystemDataId(0x0100000000000808);
        public static SystemDataId SystemVersion => new SystemDataId(0x0100000000000809);
        public static SystemDataId AvatarImage => new SystemDataId(0x010000000000080A);
        public static SystemDataId LocalNews => new SystemDataId(0x010000000000080B);
        public static SystemDataId Eula => new SystemDataId(0x010000000000080C);
        public static SystemDataId UrlBlackList => new SystemDataId(0x010000000000080D);
        public static SystemDataId TimeZoneBinar => new SystemDataId(0x010000000000080E);
        public static SystemDataId CertStoreCruiser => new SystemDataId(0x010000000000080F);
        public static SystemDataId FontNintendoExtension => new SystemDataId(0x0100000000000810);
        public static SystemDataId FontStandard => new SystemDataId(0x0100000000000811);
        public static SystemDataId FontKorean => new SystemDataId(0x0100000000000812);
        public static SystemDataId FontChineseTraditional => new SystemDataId(0x0100000000000813);
        public static SystemDataId FontChineseSimple => new SystemDataId(0x0100000000000814);
        public static SystemDataId FontBfcpx => new SystemDataId(0x0100000000000815);
        public static SystemDataId SystemUpdate => new SystemDataId(0x0100000000000816);

        public static SystemDataId FirmwareDebugSettings => new SystemDataId(0x0100000000000818);
        public static SystemDataId BootImagePackage => new SystemDataId(0x0100000000000819);
        public static SystemDataId BootImagePackageSafe => new SystemDataId(0x010000000000081A);
        public static SystemDataId BootImagePackageExFat => new SystemDataId(0x010000000000081B);
        public static SystemDataId BootImagePackageExFatSafe => new SystemDataId(0x010000000000081C);
        public static SystemDataId FatalMessage => new SystemDataId(0x010000000000081D);
        public static SystemDataId ControllerIcon => new SystemDataId(0x010000000000081E);
        public static SystemDataId PlatformConfigIcosa => new SystemDataId(0x010000000000081F);
        public static SystemDataId PlatformConfigCopper => new SystemDataId(0x0100000000000820);
        public static SystemDataId PlatformConfigHoag => new SystemDataId(0x0100000000000821);
        public static SystemDataId ControllerFirmware => new SystemDataId(0x0100000000000822);
        public static SystemDataId NgWord2 => new SystemDataId(0x0100000000000823);
        public static SystemDataId PlatformConfigIcosaMariko => new SystemDataId(0x0100000000000824);
        public static SystemDataId ApplicationBlackList => new SystemDataId(0x0100000000000825);
        public static SystemDataId RebootlessSystemUpdateVersion => new SystemDataId(0x0100000000000826);
        public static SystemDataId ContentActionTable => new SystemDataId(0x0100000000000827);

        public static SystemDataId End => new SystemDataId(0x0100000000000FFF);
    }

    public readonly struct SystemUpdateId
    {
        public readonly ulong Value;

        public SystemUpdateId(ulong value)
        {
            Value = value;
        }

        public static implicit operator DataId(SystemUpdateId id) => new DataId(id.Value);
    }

    public readonly struct SystemAppletId
    {
        public readonly ulong Value;

        public SystemAppletId(ulong value)
        {
            Value = value;
        }

        public static implicit operator ProgramId(SystemAppletId id) => new ProgramId(id.Value);

        public static bool IsSystemAppletId(ProgramId programId)
        {
            return Start <= programId && programId <= End;
        }

        public static bool IsSystemAppletId(SystemAppletId id) => true;

        public static SystemAppletId Start => new SystemAppletId(0x0100000000001000);

        public static SystemAppletId Qlaunch => new SystemAppletId(0x0100000000001000);
        public static SystemAppletId Auth => new SystemAppletId(0x0100000000001001);
        public static SystemAppletId Cabinet => new SystemAppletId(0x0100000000001002);
        public static SystemAppletId Controller => new SystemAppletId(0x0100000000001003);
        public static SystemAppletId DataErase => new SystemAppletId(0x0100000000001004);
        public static SystemAppletId Error => new SystemAppletId(0x0100000000001005);
        public static SystemAppletId NetConnect => new SystemAppletId(0x0100000000001006);
        public static SystemAppletId PlayerSelect => new SystemAppletId(0x0100000000001007);
        public static SystemAppletId Swkbd => new SystemAppletId(0x0100000000001008);
        public static SystemAppletId MiiEdit => new SystemAppletId(0x0100000000001009);
        public static SystemAppletId Web => new SystemAppletId(0x010000000000100A);
        public static SystemAppletId Shop => new SystemAppletId(0x010000000000100B);
        public static SystemAppletId OverlayDisp => new SystemAppletId(0x010000000000100C);
        public static SystemAppletId PhotoViewer => new SystemAppletId(0x010000000000100D);
        public static SystemAppletId Set => new SystemAppletId(0x010000000000100E);
        public static SystemAppletId OfflineWeb => new SystemAppletId(0x010000000000100F);
        public static SystemAppletId LoginShare => new SystemAppletId(0x0100000000001010);
        public static SystemAppletId WifiWebAuth => new SystemAppletId(0x0100000000001011);
        public static SystemAppletId Starter => new SystemAppletId(0x0100000000001012);
        public static SystemAppletId MyPage => new SystemAppletId(0x0100000000001013);
        public static SystemAppletId PlayReport => new SystemAppletId(0x0100000000001014);
        public static SystemAppletId MaintenanceMenu => new SystemAppletId(0x0100000000001015);

        public static SystemAppletId Gift => new SystemAppletId(0x010000000000101A);
        public static SystemAppletId DummyShop => new SystemAppletId(0x010000000000101B);
        public static SystemAppletId UserMigration => new SystemAppletId(0x010000000000101C);
        public static SystemAppletId Encounter => new SystemAppletId(0x010000000000101D);

        public static SystemAppletId Story => new SystemAppletId(0x0100000000001020);

        public static SystemAppletId End => new SystemAppletId(0x0100000000001FFF);
    }

    public readonly struct SystemDebugAppletId
    {
        public readonly ulong Value;

        public SystemDebugAppletId(ulong value)
        {
            Value = value;
        }

        public static implicit operator ProgramId(SystemDebugAppletId id) => new ProgramId(id.Value);

        public static bool IsSystemDebugAppletId(ProgramId programId)
        {
            return Start <= programId && programId <= End;
        }

        public static bool IsSystemDebugAppletId(SystemDebugAppletId id) => true;

        public static SystemDebugAppletId Start => new SystemDebugAppletId(0x0100000000002000);

        public static SystemDebugAppletId SnapShotDumper => new SystemDebugAppletId(0x0100000000002071);

        public static SystemDebugAppletId End => new SystemDebugAppletId(0x0100000000002FFF);
    }

    public readonly struct LibraryAppletId
    {
        public readonly ulong Value;

        public LibraryAppletId(ulong value)
        {
            Value = value;
        }

        public static implicit operator SystemAppletId(LibraryAppletId id) => new SystemAppletId(id.Value);
        public static implicit operator ProgramId(LibraryAppletId id) => new ProgramId(id.Value);

        public static bool IsLibraryAppletId(ProgramId programId)
        {
            return programId == Auth ||
                   programId == Controller ||
                   programId == Error ||
                   programId == PlayerSelect ||
                   programId == Swkbd ||
                   programId == Web ||
                   programId == Shop ||
                   programId == PhotoViewer ||
                   programId == OfflineWeb ||
                   programId == LoginShare ||
                   programId == WifiWebAuth ||
                   programId == MyPage;
        }

        public static bool IsLibraryAppletId(LibraryAppletId id) => true;

        public static LibraryAppletId Auth => new LibraryAppletId(SystemAppletId.Auth.Value);
        public static LibraryAppletId Controller => new LibraryAppletId(SystemAppletId.Controller.Value);
        public static LibraryAppletId Error => new LibraryAppletId(SystemAppletId.Error.Value);
        public static LibraryAppletId PlayerSelect => new LibraryAppletId(SystemAppletId.PlayerSelect.Value);
        public static LibraryAppletId Swkbd => new LibraryAppletId(SystemAppletId.Swkbd.Value);
        public static LibraryAppletId Web => new LibraryAppletId(SystemAppletId.Web.Value);
        public static LibraryAppletId Shop => new LibraryAppletId(SystemAppletId.Shop.Value);
        public static LibraryAppletId PhotoViewer => new LibraryAppletId(SystemAppletId.PhotoViewer.Value);
        public static LibraryAppletId OfflineWeb => new LibraryAppletId(SystemAppletId.OfflineWeb.Value);
        public static LibraryAppletId LoginShare => new LibraryAppletId(SystemAppletId.LoginShare.Value);
        public static LibraryAppletId WifiWebAuth => new LibraryAppletId(SystemAppletId.WifiWebAuth.Value);
        public static LibraryAppletId MyPage => new LibraryAppletId(SystemAppletId.MyPage.Value);
    }

    public readonly struct WebAppletId
    {
        public readonly ulong Value;

        public WebAppletId(ulong value)
        {
            Value = value;
        }

        public static implicit operator LibraryAppletId(WebAppletId id) => new LibraryAppletId(id.Value);
        public static implicit operator SystemAppletId(WebAppletId id) => new SystemAppletId(id.Value);
        public static implicit operator ProgramId(WebAppletId id) => new ProgramId(id.Value);

        public static bool IsWebAppletId(ProgramId programId)
        {
            return programId == Web ||
                   programId == Shop ||
                   programId == OfflineWeb ||
                   programId == LoginShare ||
                   programId == WifiWebAuth;
        }

        public static bool IsWebAppletId(WebAppletId id) => true;

        public static WebAppletId Web => new WebAppletId(LibraryAppletId.Web.Value);
        public static WebAppletId Shop => new WebAppletId(LibraryAppletId.Shop.Value);
        public static WebAppletId OfflineWeb => new WebAppletId(LibraryAppletId.OfflineWeb.Value);
        public static WebAppletId LoginShare => new WebAppletId(LibraryAppletId.LoginShare.Value);
        public static WebAppletId WifiWebAuth => new WebAppletId(LibraryAppletId.WifiWebAuth.Value);
    }

    public readonly struct SystemApplicationId
    {
        public readonly ulong Value;

        public SystemApplicationId(ulong value)
        {
            Value = value;
        }

        public static implicit operator ProgramId(SystemApplicationId id) => new ProgramId(id.Value);
    }
}
