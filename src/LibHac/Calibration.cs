using System.IO;
using System.Text;

namespace LibHac
{
    public class Calibration
    {
        public const string ExpectedMagic = "CAL0";

        public string Magic;
        public int Version;
        public int CalibDataSize;
        public short Model;
        public short Revision;
        public byte[] CalibDataSha256;
        public string ConfigId1;
        public byte[] Reserved;
        public int WlanCountryCodesNum;
        public int WlanCountryCodesLastIdx;
        public string[] WlanCountryCodes;
        public byte[] WlanMacAddr;
        public byte[] BdAddr;
        public byte[] AccelerometerOffset;
        public byte[] AccelerometerScale;
        public byte[] GyroscopeOffset;
        public byte[] GyroscopeScale;
        public string SerialNumber;
        public byte[] DeviceKeyEccP256;
        public byte[] DeviceCertEccP256;
        public byte[] DeviceKeyEccB233;
        public byte[] DeviceCertEccB233;
        public byte[] EticketKeyEccP256;
        public byte[] EticketCertEccP256;
        public byte[] EticketKeyEccB233;
        public byte[] EticketCertEccB233;
        public byte[] SslKey;
        public int SslCertSize;
        public byte[] SslCert;
        public byte[] SslCertSha256;
        public byte[] RandomNumber;
        public byte[] RandomNumberSha256;
        public byte[] GamecardKey;
        public byte[] GamecardCert;
        public byte[] GamecardCertSha256;
        public byte[] EticketKeyRsa;
        public byte[] EticketCertRsa;
        public string BatteryLot;
        public byte[] SpeakerCalibValue;
        public int RegionCode;
        public byte[] AmiiboKey;
        public byte[] AmiiboCertEcqv;
        public byte[] AmiiboCertEcdsa;
        public byte[] AmiiboKeyEcqvBls;
        public byte[] AmiiboCertEcqvBls;
        public byte[] AmiiboRootCertEcqvBls;
        public int ProductModel;
        public byte[] ColorVariation;
        public byte[] LcdBacklightBrightnessMapping;
        public byte[] DeviceExtKeyEccB233;
        public byte[] EticketExtKeyEccP256;
        public byte[] EticketExtKeyEccB233;
        public byte[] EticketExtKeyRsa;
        public byte[] SslExtKey;
        public byte[] GamecardExtKey;
        public int LcdVendorId;
        public byte[] ExtendedRsa2048DeviceKey;
        public byte[] Rsa2048DeviceCertificate;
        public byte[] UsbTypeCPowerSourceCircuitVersion;
        public int HomeMenuSchemeSubColor;
        public int HomeMenuSchemeBezelColor;
        public int HomeMenuSchemeMainColor1;
        public int HomeMenuSchemeMainColor2;
        public int HomeMenuSchemeMainColor3;
        public byte[] AnalogStickModuleTypeL;
        public byte[] AnalogStickModelParameterL;
        public byte[] AnalogStickFactoryCalibrationL;
        public byte[] AnalogStickModuleTypeR;
        public byte[] AnalogStickModelParameterR;
        public byte[] AnalogStickFactoryCalibrationR;
        public byte[] ConsoleSixAxisSensorModuleType;
        public byte[] ConsoleSixAxisSensorHorizontalOffset;
        public byte[] BatteryVersion;
        public int HomeMenuSchemeModel;
        public byte[] ConsoleSixAxisSensorMountType;

        public Calibration(Stream stream)
        {
            using var reader = new BinaryReader(stream, Encoding.Default, true);

            stream.Position = 0x0;
            Magic = reader.ReadUtf8(0x4);

            stream.Position = 0x4;
            Version = reader.ReadInt32();

            stream.Position = 0x8;
            CalibDataSize = reader.ReadInt32();

            stream.Position = 0xC;
            Model = reader.ReadInt16();

            stream.Position = 0xE;
            Revision = reader.ReadInt16();

            stream.Position = 0x20;
            CalibDataSha256 = reader.ReadBytes(0x20);

            stream.Position = 0x40;
            ConfigId1 = reader.ReadUtf8Z(0x1E);

            stream.Position = 0x60;
            Reserved = reader.ReadBytes(0x20);

            stream.Position = 0x80;
            WlanCountryCodesNum = reader.ReadInt32();
            WlanCountryCodesLastIdx = reader.ReadInt32();
            WlanCountryCodes = new string[WlanCountryCodesNum];
            for (int i = 0; i < WlanCountryCodesNum; i++)
            {
                stream.Position = 0x88 + i * 4;
                WlanCountryCodes[i] = reader.ReadUtf8Z();
            }
            stream.Position = 0x210;
            WlanMacAddr = reader.ReadBytes(0x6);

            stream.Position = 0x220;
            BdAddr = reader.ReadBytes(0x6);
            stream.Position = 0x230;
            AccelerometerOffset = reader.ReadBytes(0x6);
            stream.Position = 0x238;
            AccelerometerScale = reader.ReadBytes(0x6);
            stream.Position = 0x240;
            GyroscopeOffset = reader.ReadBytes(0x6);
            stream.Position = 0x248;
            GyroscopeScale = reader.ReadBytes(0x6);

            stream.Position = 0x250;
            SerialNumber = reader.ReadUtf8Z(0x18);

            stream.Position = 0x270;
            DeviceKeyEccP256 = reader.ReadBytes(0x30);
            stream.Position = 0x2B0;
            DeviceCertEccP256 = reader.ReadBytes(0x180);
            stream.Position = 0x440;
            DeviceKeyEccB233 = reader.ReadBytes(0x30);
            stream.Position = 0x480;
            DeviceCertEccB233 = reader.ReadBytes(0x180);
            stream.Position = 0x610;
            EticketKeyEccP256 = reader.ReadBytes(0x30);
            stream.Position = 0x650;
            EticketCertEccP256 = reader.ReadBytes(0x180);
            stream.Position = 0x7E0;
            EticketKeyEccB233 = reader.ReadBytes(0x30);
            stream.Position = 0x820;
            EticketCertEccB233 = reader.ReadBytes(0x180);

            stream.Position = 0x9B0;
            SslKey = reader.ReadBytes(0x110);
            stream.Position = 0xAD0;
            SslCertSize = reader.ReadInt32();
            stream.Position = 0x0AE0;
            SslCert = reader.ReadBytes(SslCertSize);
            stream.Position = 0x12E0;
            SslCertSha256 = reader.ReadBytes(0x20);

            stream.Position = 0x1300;
            RandomNumber = reader.ReadBytes(0x1000);
            stream.Position = 0x2300;
            RandomNumberSha256 = reader.ReadBytes(0x20);

            stream.Position = 0x2320;
            GamecardKey = reader.ReadBytes(0x110);
            stream.Position = 0x2440;
            GamecardCert = reader.ReadBytes(0x400);
            stream.Position = 0x2840;
            GamecardCertSha256 = reader.ReadBytes(0x20);

            stream.Position = 0x2860;
            EticketKeyRsa = reader.ReadBytes(0x220);
            stream.Position = 0x2A90;
            EticketCertRsa = reader.ReadBytes(0x240);

            stream.Position = 0x2CE0;
            BatteryLot = reader.ReadUtf8Z(0x18);

            stream.Position = 0x2D00;
            SpeakerCalibValue = reader.ReadBytes(0x800);

            stream.Position = 0x3510;
            RegionCode = reader.ReadInt32();

            stream.Position = 0x3520;
            AmiiboKey = reader.ReadBytes(0x50);
            stream.Position = 0x3580;
            AmiiboCertEcqv = reader.ReadBytes(0x14);
            stream.Position = 0x35A0;
            AmiiboCertEcdsa = reader.ReadBytes(0x70);
            stream.Position = 0x3620;
            AmiiboKeyEcqvBls = reader.ReadBytes(0x40);
            stream.Position = 0x3670;
            AmiiboCertEcqvBls = reader.ReadBytes(0x20);
            stream.Position = 0x36A0;
            AmiiboRootCertEcqvBls = reader.ReadBytes(0x90);

            stream.Position = 0x3740;
            ProductModel = reader.ReadInt32();
            stream.Position = 0x3750;
            ColorVariation = reader.ReadBytes(0x06);
            stream.Position = 0x3760;
            LcdBacklightBrightnessMapping = reader.ReadBytes(0x0C);

            stream.Position = 0x3770;
            DeviceExtKeyEccB233 = reader.ReadBytes(0x50);
            stream.Position = 0x37D0;
            EticketExtKeyEccP256 = reader.ReadBytes(0x50);
            stream.Position = 0x3830;
            EticketExtKeyEccB233 = reader.ReadBytes(0x50);
            stream.Position = 0x3890;
            EticketExtKeyRsa = reader.ReadBytes(0x240);
            stream.Position = 0x3AE0;
            SslExtKey = reader.ReadBytes(0x130);
            stream.Position = 0x3C20;
            GamecardExtKey = reader.ReadBytes(0x130);

            stream.Position = 0x3D60;
            LcdVendorId = reader.ReadInt32();

            stream.Position = 0x3D70;
            ExtendedRsa2048DeviceKey = reader.ReadBytes(0x240);
            stream.Position = 0x3FC0;
            Rsa2048DeviceCertificate = reader.ReadBytes(0x240);

            stream.Position = 0x4210;
            UsbTypeCPowerSourceCircuitVersion = reader.ReadBytes(0x1);

            stream.Position = 0x4220;
            HomeMenuSchemeSubColor = reader.ReadInt32();
            stream.Position = 0x4230;
            HomeMenuSchemeBezelColor = reader.ReadInt32();
            stream.Position = 0x4240;
            HomeMenuSchemeMainColor1 = reader.ReadInt32();
            stream.Position = 0x4250;
            HomeMenuSchemeMainColor2 = reader.ReadInt32();
            stream.Position = 0x4260;
            HomeMenuSchemeMainColor3 = reader.ReadInt32();

            stream.Position = 0x4270;
            AnalogStickModuleTypeL = reader.ReadBytes(0x1);
            stream.Position = 0x4280;
            AnalogStickModelParameterL = reader.ReadBytes(0x12);
            stream.Position = 0x42A0;
            AnalogStickFactoryCalibrationL = reader.ReadBytes(0x9);
            stream.Position = 0x42B0;
            AnalogStickModuleTypeR = reader.ReadBytes(0x1);
            stream.Position = 0x42C0;
            AnalogStickModelParameterR = reader.ReadBytes(0x12);
            stream.Position = 0x42E0;
            AnalogStickFactoryCalibrationR = reader.ReadBytes(0x9);

            stream.Position = 0x42F0;
            ConsoleSixAxisSensorModuleType = reader.ReadBytes(0x1);
            stream.Position = 0x4300;
            ConsoleSixAxisSensorHorizontalOffset = reader.ReadBytes(0x6);

            stream.Position = 0x4310;
            BatteryVersion = reader.ReadBytes(0x1);

            stream.Position = 0x4330;
            HomeMenuSchemeModel = reader.ReadInt32();

            stream.Position = 0x4340;
            ConsoleSixAxisSensorMountType = reader.ReadBytes(0x1);
        }
    }
}