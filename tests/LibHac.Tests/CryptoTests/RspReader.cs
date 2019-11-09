using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace LibHac.Tests.CryptoTests
{
    public class RspReader
    {
        private StreamReader Reader { get; }

        public RspReader(Stream stream)
        {
            Reader = new StreamReader(stream);
        }

        public IEnumerable<EncryptionTestVector> GetTestVectors()
        {
            string line;
            bool isEncryptType = false;

            var testVector = new EncryptionTestVector();
            bool canOutputVector = false;

            while ((line = Reader.ReadLine()?.Trim()) != null)
            {
                if (line.Length == 0)
                {
                    if (canOutputVector)
                    {
                        testVector.Encrypt = isEncryptType;

                        yield return testVector;

                        testVector = new EncryptionTestVector();
                        canOutputVector = false;
                    }

                    continue;
                }

                if (line[0] == '#') continue;

                if (line[0] == '[')
                {
                    if (line == "[ENCRYPT]") isEncryptType = true;
                    if (line == "[DECRYPT]") isEncryptType = false;

                    continue;
                }

                string[] kvp = line.Split(new[] { " = " }, StringSplitOptions.None);
                if (kvp.Length != 2) throw new InvalidDataException();

                canOutputVector = true;

                switch (kvp[0])
                {
                    case "COUNT":
                        testVector.Count = int.Parse(kvp[1]);
                        break;
                    case "KEY":
                        testVector.Key = kvp[1].ToBytes();
                        break;
                    case "IV":
                        testVector.Iv = kvp[1].ToBytes();
                        break;
                    case "PLAINTEXT":
                        testVector.PlainText = kvp[1].ToBytes();
                        break;
                    case "CIPHERTEXT":
                        testVector.CipherText = kvp[1].ToBytes();
                        break;
                }
            }
        }

        public static TheoryData<EncryptionTestVector> ReadTestVectors(bool getEncryptTests, params string[] filenames)
        {
            IEnumerable<string> resourcePaths = filenames.Select(x => $"LibHac.Tests.CryptoTests.TestVectors.{x}");
            var testVectors = new TheoryData<EncryptionTestVector>();

            foreach (string path in resourcePaths)
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(path))
                {
                    var reader = new RspReader(stream);

                    foreach (EncryptionTestVector tv in reader.GetTestVectors().Where(x => x.Encrypt == getEncryptTests))
                    {
                        testVectors.Add(tv);
                    }
                }
            }

            return testVectors;
        }
    }

    public class EncryptionTestVector
    {
        public bool Encrypt { get; set; }
        public int Count { get; set; }
        public byte[] Key { get; set; }
        public byte[] Iv { get; set; }
        public byte[] PlainText { get; set; }
        public byte[] CipherText { get; set; }
    }
}
