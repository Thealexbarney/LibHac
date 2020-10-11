using System.Linq;
using LibHac.FsSystem;
using LibHac.Util;
using Xunit;

namespace LibHac.Tests
{
    public class AesXts
    {
        public static readonly TheoryData<TestData> TestVectors = new TheoryData<TestData>
        {
            // #1 32 byte key, 32 byte PTX 
            new TestData
            {
                Key1 = "00000000000000000000000000000000".ToBytes(),
                Key2 = "00000000000000000000000000000000".ToBytes(),
                PlainText  = "0000000000000000000000000000000000000000000000000000000000000000".ToBytes(),
                CipherText = "917CF69EBD68B2EC9B9FE9A3EADDA692CD43D2F59598ED858C02C2652FBF922E".ToBytes(),
                Sector = 0
            },

            // #2, 32 byte key, 32 byte PTX 
            new TestData
            {
                Sector = 0x3333333333,
                Key1 = "11111111111111111111111111111111".ToBytes(),
                Key2 = "22222222222222222222222222222222".ToBytes(),
                PlainText  = "4444444444444444444444444444444444444444444444444444444444444444".ToBytes(),
                CipherText = "44BEC82FFB76AEFDFBC96DFE61E192CCFA2213677C8F4FD6E4F18F7EBB69382F".ToBytes()
            },

            // #5 from xts.7, 32 byte key, 32 byte PTX 
            new TestData
            {
                Sector = 0x123456789A,
                Key1 = "FFFEFDFCFBFAF9F8F7F6F5F4F3F2F1F0".ToBytes(),
                Key2 = "BFBEBDBCBBBAB9B8B7B6B5B4B3B2B1B0".ToBytes(),
                PlainText  = "4444444444444444444444444444444444444444444444444444444444444444".ToBytes(),
                CipherText = "C11839D636AD8BE5A116E48C70227763DABD3C2D1383C5DD15B2572AAA992C40".ToBytes()
            },

            // #4, 32 byte key, 512 byte PTX  
            new TestData
            {
                Sector = 0,
                Key1 = "27182818284590452353602874713526".ToBytes(),
                Key2 = "31415926535897932384626433832795".ToBytes(),
                PlainText  = ("000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F" +
                              "202122232425262728292A2B2C2D2E2F303132333435363738393A3B3C3D3E3F" +
                              "404142434445464748494A4B4C4D4E4F505152535455565758595A5B5C5D5E5F" +
                              "606162636465666768696A6B6C6D6E6F707172737475767778797A7B7C7D7E7F" +
                              "808182838485868788898A8B8C8D8E8F909192939495969798999A9B9C9D9E9F" +
                              "A0A1A2A3A4A5A6A7A8A9AAABACADAEAFB0B1B2B3B4B5B6B7B8B9BABBBCBDBEBF" +
                              "C0C1C2C3C4C5C6C7C8C9CACBCCCDCECFD0D1D2D3D4D5D6D7D8D9DADBDCDDDEDF" +
                              "E0E1E2E3E4E5E6E7E8E9EAEBECEDEEEFF0F1F2F3F4F5F6F7F8F9FAFBFCFDFEFF" +
                              "000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F" +
                              "202122232425262728292A2B2C2D2E2F303132333435363738393A3B3C3D3E3F" +
                              "404142434445464748494A4B4C4D4E4F505152535455565758595A5B5C5D5E5F" +
                              "606162636465666768696A6B6C6D6E6F707172737475767778797A7B7C7D7E7F" +
                              "808182838485868788898A8B8C8D8E8F909192939495969798999A9B9C9D9E9F" +
                              "A0A1A2A3A4A5A6A7A8A9AAABACADAEAFB0B1B2B3B4B5B6B7B8B9BABBBCBDBEBF" +
                              "C0C1C2C3C4C5C6C7C8C9CACBCCCDCECFD0D1D2D3D4D5D6D7D8D9DADBDCDDDEDF" +
                              "E0E1E2E3E4E5E6E7E8E9EAEBECEDEEEFF0F1F2F3F4F5F6F7F8F9FAFBFCFDFEFF").ToBytes(),
                CipherText = ("27A7479BEFA1D476489F308CD4CFA6E2A96E4BBE3208FF25287DD3819616E89C" +
                              "C78CF7F5E543445F8333D8FA7F56000005279FA5D8B5E4AD40E736DDB4D35412" +
                              "328063FD2AAB53E5EA1E0A9F332500A5DF9487D07A5C92CC512C8866C7E860CE" +
                              "93FDF166A24912B422976146AE20CE846BB7DC9BA94A767AAEF20C0D61AD0265" +
                              "5EA92DC4C4E41A8952C651D33174BE51A10C421110E6D81588EDE82103A252D8" +
                              "A750E8768DEFFFED9122810AAEB99F9172AF82B604DC4B8E51BCB08235A6F434" +
                              "1332E4CA60482A4BA1A03B3E65008FC5DA76B70BF1690DB4EAE29C5F1BADD03C" +
                              "5CCF2A55D705DDCD86D449511CEB7EC30BF12B1FA35B913F9F747A8AFD1B130E" +
                              "94BFF94EFFD01A91735CA1726ACD0B197C4E5B03393697E126826FB6BBDE8ECC" +
                              "1E08298516E2C9ED03FF3C1B7860F6DE76D4CECD94C8119855EF5297CA67E9F3" +
                              "E7FF72B1E99785CA0A7E7720C5B36DC6D72CAC9574C8CBBC2F801E23E56FD344" +
                              "B07F22154BEBA0F08CE8891E643ED995C94D9A69C9F1B5F499027A78572AEEBD" +
                              "74D20CC39881C213EE770B1010E4BEA718846977AE119F7A023AB58CCA0AD752" +
                              "AFE656BB3C17256A9F6E9BF19FDD5A38FC82BBE872C5539EDB609EF4F79C203E" +
                              "BB140F2E583CB2AD15B4AA5B655016A8449277DBD477EF2C8D6C017DB738B18D" +
                              "EB4A427D1923CE3FF262735779A418F20A282DF920147BEABE421EE5319D0568").ToBytes()
            },

            // #7, 32 byte key, 17 byte PTX 
            new TestData
            {
                Sector = 0x123456789A,
                Key1 = "FFFEFDFCFBFAF9F8F7F6F5F4F3F2F1F0".ToBytes(),
                Key2 = "BFBEBDBCBBBAB9B8B7B6B5B4B3B2B1B0".ToBytes(),
                PlainText = "000102030405060708090A0B0C0D0E0F10".ToBytes(),
                CipherText = "9E61715809A74B7E0EF033CD86181404C2".ToBytes()
            },

            // #15, 32 byte key, 25 byte PTX 
            new TestData
            {
                Sector = 0x123456789A,
                Key1 = "FFFEFDFCFBFAF9F8F7F6F5F4F3F2F1F0".ToBytes(),
                Key2 = "BFBEBDBCBBBAB9B8B7B6B5B4B3B2B1B0".ToBytes(),
                PlainText  = "000102030405060708090A0B0C0D0E0F101112131415161718".ToBytes(),
                CipherText = "5D0B4A86EC5A91FB849D0F826A316222C274AD93FC68C2C101".ToBytes()
            },

            // #21, 32 byte key, 31 byte PTX 
            new TestData
            {
                Sector = 0x123456789A,
                Key1 = "FFFEFDFCFBFAF9F8F7F6F5F4F3F2F1F0".ToBytes(),
                Key2 = "BFBEBDBCBBBAB9B8B7B6B5B4B3B2B1B0".ToBytes(),
                PlainText  = "000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E".ToBytes(),
                CipherText = "42673C897D4F532CF8AA65EEB4D5B6F5C274AD93FC68C2C1015D904F33FF95".ToBytes()
            }
        };

        [Theory]
        [MemberData(nameof(TestVectors))]
        public static void Encrypt(TestData data) => TestTransform(data, false);

        [Theory]
        [MemberData(nameof(TestVectors))]
        public static void Decrypt(TestData data) => TestTransform(data, true);

        private static void TestTransform(TestData data, bool decrypting)
        {
            var transform = new Aes128XtsTransform(data.Key1, data.Key2, decrypting);
            byte[] transformed = data.GetInitialText(decrypting).ToArray();

            transform.TransformBlock(transformed, 0, transformed.Length, data.Sector);
            Assert.Equal(data.GetTransformedText(decrypting), transformed);
        }

        public struct TestData
        {
            public byte[] CipherText;
            public byte[] PlainText;
            public byte[] Key1;
            public byte[] Key2;
            public ulong Sector;

            public byte[] GetInitialText(bool decrypting) => decrypting ? CipherText : PlainText;
            public byte[] GetTransformedText(bool decrypting) => decrypting ? PlainText : CipherText;
        }
    }
}
