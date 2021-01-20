using System;
using LibHac.Crypto;
using LibHac.Util;
using Xunit;

namespace LibHac.Tests
{
    public class AesCmac
    {
        public static readonly TheoryData<TestData> TestVectors = new TheoryData<TestData>
        {
            new TestData
            {
                Key = "2B7E151628AED2A6ABF7158809CF4F3C".ToBytes(),
                Message = "".ToBytes(),
                Expected = "BB1D6929E95937287FA37D129B756746".ToBytes()
            },
            new TestData
            {
                Key = "2B7E151628AED2A6ABF7158809CF4F3C".ToBytes(),
                Message = "6BC1BEE22E409F96E93D7E117393172A".ToBytes(),
                Expected = "070A16B46B4D4144F79BDD9DD04A287C".ToBytes(),
                Length = 0x10
            },
            new TestData
            {
                Key = "2B7E151628AED2A6ABF7158809CF4F3C".ToBytes(),
                Message = "6BC1BEE22E409F96E93D7E117393172AAE2D8A571E03AC9C9EB76FAC45AF8E5130C81C46A35CE411".ToBytes(),
                Expected = "DFA66747DE9AE63030CA32611497C827".ToBytes(),
                Length = 0x28
            },
            new TestData
            {
                Key = "2B7E151628AED2A6ABF7158809CF4F3C".ToBytes(),
                Message = "6BC1BEE22E409F96E93D7E117393172AAE2D8A571E03AC9C9EB76FAC45AF8E5130C81C46A35CE411E5FBC1191A0A52EFF69F2445DF4F9B17AD2B417BE66C3710".ToBytes(),
                Expected = "51F0BEBF7E3B9D92FC49741779363CFE".ToBytes(),
                Length = 0x40
            },
            new TestData
            {
                Key = "2B7E151628AED2A6ABF7158809CF4F3C".ToBytes(),
                Message = "00000000006BC1BEE22E409F96E93D7E117393172A0000000000".ToBytes(),
                Expected = "070A16B46B4D4144F79BDD9DD04A287C".ToBytes(),
                Start = 5,
                Length = 0x10
            },
            new TestData
            {
                Key = "2B7E151628AED2A6ABF7158809CF4F3C".ToBytes(),
                Message = "00000000006BC1BEE22E409F96E93D7E117393172A0000000000000000000000".ToBytes(),
                Expected = "070A16B46B4D4144F79BDD9DD04A287C".ToBytes(),
                Start = 5,
                Length = 0x10
            }
        };

        [Theory]
        [MemberData(nameof(TestVectors))]
        public static void TestCmacTestVectors(TestData data)
        {
            byte[] actual = new byte[0x10];

            Aes.CalculateCmac(actual, data.Message.AsSpan(data.Start, data.Length), data.Key);

            Assert.Equal(data.Expected, actual);
        }

        public struct TestData
        {
            public byte[] Key;
            public byte[] Message;
            public byte[] Expected;
            public int Start;
            public int Length;
        }
    }
}
