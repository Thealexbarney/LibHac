using System;
using System.Diagnostics;
using System.Linq;
using LibHac;
using LibHac.Crypto2;
using LibHac.Fs;
using LibHac.FsSystem;

namespace hactoolnet
{
    internal static class ProcessBench
    {
        private const int Size = 1024 * 1024 * 10;
        private const int Iterations = 100;
        private const int BlockSizeBlocked = 0x10;
        private const int BlockSizeSeparate = 0x10;

        private static void CopyBenchmark(IStorage src, IStorage dst, int iterations, string label, IProgressReport logger)
        {
            // Warmup
            src.CopyTo(dst);

            logger.SetTotal(iterations);

            Stopwatch encryptWatch = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                src.CopyTo(dst);
                logger.ReportAdd(1);
            }
            encryptWatch.Stop();
            logger.SetTotal(0);

            src.GetSize(out long srcSize).ThrowIfFailure();

            string rate = Util.GetBytesReadable((long)(srcSize * iterations / encryptWatch.Elapsed.TotalSeconds));
            logger.LogMessage($"{label}{rate}/s");
        }

        private static void CipherBenchmark(ReadOnlySpan<byte> src, Span<byte> dst, Func<ICipher> cipherGenerator,
            int iterations, string label, IProgressReport logger)
        {
            cipherGenerator().Transform(src, dst);

            var watch = new Stopwatch();
            var runTimes = new double[iterations];

            logger.SetTotal(iterations);

            for (int i = 0; i < iterations; i++)
            {
                ICipher cipher = cipherGenerator();

                watch.Restart();
                cipher.Transform(src, dst);
                watch.Stop();

                logger.ReportAdd(1);
                runTimes[i] = watch.Elapsed.TotalSeconds;
            }

            logger.SetTotal(0);

            long srcSize = src.Length;

            double fastestRun = runTimes.Min();
            double averageRun = runTimes.Average();
            double slowestRun = runTimes.Max();

            string fastestRate = Util.GetBytesReadable((long)(srcSize / fastestRun));
            string averageRate = Util.GetBytesReadable((long)(srcSize / averageRun));
            string slowestRate = Util.GetBytesReadable((long)(srcSize / slowestRun));

            logger.LogMessage($"{label}{averageRate}/s, fastest run: {fastestRate}/s, slowest run: {slowestRate}/s");
        }

        private static void CipherBenchmarkBlocked(ReadOnlySpan<byte> src, Span<byte> dst, Func<ICipher> cipherGenerator,
            int iterations, string label, IProgressReport logger)
        {
            cipherGenerator().Transform(src, dst);

            var watch = new Stopwatch();
            var runTimes = new double[iterations];

            logger.SetTotal(iterations);

            int blockCount = src.Length / BlockSizeBlocked;

            for (int i = 0; i < iterations; i++)
            {
                ICipher cipher = cipherGenerator();

                watch.Restart();

                for (int b = 0; b < blockCount; b++)
                {
                    cipher.Transform(src.Slice(b * BlockSizeBlocked, BlockSizeBlocked),
                        dst.Slice(b * BlockSizeBlocked, BlockSizeBlocked));
                }

                watch.Stop();

                logger.ReportAdd(1);
                runTimes[i] = watch.Elapsed.TotalSeconds;
            }

            logger.SetTotal(0);

            long srcSize = src.Length;

            double fastestRun = runTimes.Min();
            double averageRun = runTimes.Average();
            double slowestRun = runTimes.Max();

            string fastestRate = Util.GetBytesReadable((long)(srcSize / fastestRun));
            string averageRate = Util.GetBytesReadable((long)(srcSize / averageRun));
            string slowestRate = Util.GetBytesReadable((long)(srcSize / slowestRun));

            logger.LogMessage($"{label}{averageRate}/s, fastest run: {fastestRate}/s, slowest run: {slowestRate}/s");
        }

        private delegate void CipherTaskSeparate(ReadOnlySpan<byte> input, Span<byte> output, ReadOnlySpan<byte> key1,
            ReadOnlySpan<byte> key2, ReadOnlySpan<byte> iv, bool preferDotNetCrypto = false);

        // Benchmarks encrypting each block separately, initializing a new cipher object for each one
        private static void CipherBenchmarkSeparate(ReadOnlySpan<byte> src, Span<byte> dst, CipherTaskSeparate function,
            int iterations, string label, bool dotNetCrypto, IProgressReport logger)
        {
            Debug.Assert(src.Length == dst.Length);

            var watch = new Stopwatch();
            var runTimes = new double[iterations];

            ReadOnlySpan<byte> key1 = stackalloc byte[0x10];
            ReadOnlySpan<byte> key2 = stackalloc byte[0x10];
            ReadOnlySpan<byte> iv = stackalloc byte[0x10];

            logger.SetTotal(iterations);

            const int blockSize = BlockSizeSeparate;
            int blockCount = src.Length / blockSize;

            for (int i = 0; i < iterations; i++)
            {
                watch.Restart();

                for (int b = 0; b < blockCount; b++)
                {
                    function(src.Slice(b * blockSize, blockSize), dst.Slice(b * blockSize, blockSize),
                        key1, key2, iv, dotNetCrypto);
                }

                watch.Stop();

                logger.ReportAdd(1);
                runTimes[i] = watch.Elapsed.TotalSeconds;
            }

            logger.SetTotal(0);

            long srcSize = src.Length;

            double fastestRun = runTimes.Min();
            double averageRun = runTimes.Average();
            double slowestRun = runTimes.Max();

            string fastestRate = Util.GetBytesReadable((long)(srcSize / fastestRun));
            string averageRate = Util.GetBytesReadable((long)(srcSize / averageRun));
            string slowestRate = Util.GetBytesReadable((long)(srcSize / slowestRun));

            logger.LogMessage($"{label}{averageRate}/s, fastest run: {fastestRate}/s, slowest run: {slowestRate}/s");
        }

        private static void RunCipherBenchmark(Func<ICipher> cipherNet, Func<ICipher> cipherLibHac,
            CipherTaskSeparate function, bool benchBlocked, string label, IProgressReport logger)
        {
            var srcData = new byte[Size];

            var dstDataLh = new byte[Size];
            var dstDataNet = new byte[Size];
            var dstDataBlockedLh = new byte[Size];
            var dstDataBlockedNet = new byte[Size];
            var dstDataSeparateLh = new byte[Size];
            var dstDataSeparateNet = new byte[Size];

            logger.LogMessage(string.Empty);
            logger.LogMessage(label);

            if (Aes.IsAesNiSupported())
                CipherBenchmark(srcData, dstDataLh, cipherLibHac, Iterations, "LibHac impl: ", logger);
            CipherBenchmark(srcData, dstDataNet, cipherNet, Iterations, ".NET impl:   ", logger);

            if (benchBlocked)
            {
                if (Aes.IsAesNiSupported())
                    CipherBenchmarkBlocked(srcData, dstDataBlockedLh, cipherLibHac, Iterations / 5,
                        "LibHac impl (blocked): ", logger);

                CipherBenchmarkBlocked(srcData, dstDataBlockedNet, cipherNet, Iterations / 5, ".NET impl (blocked):   ",
                    logger);
            }

            if (function != null)
            {
                if (Aes.IsAesNiSupported())
                    CipherBenchmarkSeparate(srcData, dstDataSeparateLh, function, Iterations / 5,
                        "LibHac impl (separate): ", false, logger);

                CipherBenchmarkSeparate(srcData, dstDataSeparateNet, function, Iterations / 20,
                    ".NET impl (separate): ", true, logger);
            }

            if (Aes.IsAesNiSupported())
            {
                logger.LogMessage($"{dstDataLh.SequenceEqual(dstDataNet)}");

                if (benchBlocked)
                {
                    logger.LogMessage($"{dstDataLh.SequenceEqual(dstDataBlockedLh)}");
                    logger.LogMessage($"{dstDataLh.SequenceEqual(dstDataBlockedNet)}");
                }

                if (function != null)
                {
                    logger.LogMessage($"{dstDataLh.SequenceEqual(dstDataSeparateLh)}");
                    logger.LogMessage($"{dstDataLh.SequenceEqual(dstDataSeparateNet)}");
                }
            }
        }

        public static void Process(Context ctx)
        {
            switch (ctx.Options.BenchType?.ToLower())
            {
                case "aesctr":
                {
                    IStorage decStorage = new MemoryStorage(new byte[Size]);
                    IStorage encStorage = new Aes128CtrStorage(new MemoryStorage(new byte[Size]), new byte[0x10], new byte[0x10], true);

                    CopyBenchmark(decStorage, encStorage, Iterations, "MemoryStorage Encrypt: ", ctx.Logger);
                    CopyBenchmark(encStorage, decStorage, Iterations, "MemoryStorage Decrypt: ", ctx.Logger);

                    decStorage = new NullStorage(Size);
                    encStorage = new Aes128CtrStorage(new NullStorage(Size), new byte[0x10], new byte[0x10], true);

                    CopyBenchmark(decStorage, encStorage, Iterations, "NullStorage Encrypt: ", ctx.Logger);
                    CopyBenchmark(encStorage, decStorage, Iterations, "NullStorage Decrypt: ", ctx.Logger);

                    decStorage = new MemoryStorage(new byte[Size]);
                    encStorage = new CachedStorage(new Aes128CtrStorage(new MemoryStorage(new byte[Size]), new byte[0x10], new byte[0x10], true), 0x4000, 4, true);

                    CopyBenchmark(decStorage, encStorage, Iterations, "CachedStorage Encrypt: ", ctx.Logger);
                    CopyBenchmark(encStorage, decStorage, Iterations, "CachedStorage Decrypt: ", ctx.Logger);

                    break;
                }

                case "aesxts":
                {
                    IStorage decStorage = new MemoryStorage(new byte[Size]);
                    IStorage encStorage = new Aes128XtsStorage(new MemoryStorage(new byte[Size]), new byte[0x20], 81920, true);

                    CopyBenchmark(decStorage, encStorage, Iterations, "MemoryStorage Encrypt: ", ctx.Logger);
                    CopyBenchmark(encStorage, decStorage, Iterations, "MemoryStorage Decrypt: ", ctx.Logger);

                    decStorage = new NullStorage(Size);
                    encStorage = new Aes128XtsStorage(new NullStorage(Size), new byte[0x20], 81920, true);

                    CopyBenchmark(decStorage, encStorage, Iterations, "NullStorage Encrypt: ", ctx.Logger);
                    CopyBenchmark(encStorage, decStorage, Iterations, "NullStorage Decrypt: ", ctx.Logger);

                    decStorage = new MemoryStorage(new byte[Size]);
                    encStorage = new CachedStorage(new Aes128XtsStorage(new MemoryStorage(new byte[Size]), new byte[0x20], 0x4000, true), 4, true);

                    CopyBenchmark(decStorage, encStorage, Iterations, "CachedStorage Encrypt: ", ctx.Logger);
                    CopyBenchmark(encStorage, decStorage, Iterations, "CachedStorage Decrypt: ", ctx.Logger);
                    break;
                }

                case "aesecbnew":
                {
                    Func<ICipher> encryptorNet = () => Aes.CreateEcbEncryptor(new byte[0x10], true);
                    Func<ICipher> encryptorLh = () => Aes.CreateEcbEncryptor(new byte[0x10]);
                    CipherTaskSeparate encrypt = (input, output, key1, key2, iv, crypto) =>
                        Aes.EncryptEcb128(input, output, key1, crypto);

                    RunCipherBenchmark(encryptorNet, encryptorLh, encrypt, true, "AES-ECB encrypt", ctx.Logger);

                    Func<ICipher> decryptorNet = () => Aes.CreateEcbDecryptor(new byte[0x10], true);
                    Func<ICipher> decryptorLh = () => Aes.CreateEcbDecryptor(new byte[0x10]);
                    CipherTaskSeparate decrypt = (input, output, key1, key2, iv, crypto) =>
                        Aes.DecryptEcb128(input, output, key1, crypto);

                    RunCipherBenchmark(decryptorNet, decryptorLh, decrypt, true, "AES-ECB decrypt", ctx.Logger);

                    break;
                }
                case "aescbcnew":
                {
                    Func<ICipher> encryptorNet = () => Aes.CreateCbcEncryptor(new byte[0x10], new byte[0x10], true);
                    Func<ICipher> encryptorLh = () => Aes.CreateCbcEncryptor(new byte[0x10], new byte[0x10]);
                    CipherTaskSeparate encrypt = (input, output, key1, key2, iv, crypto) =>
                        Aes.EncryptCbc128(input, output, key1, iv, crypto);

                    RunCipherBenchmark(encryptorNet, encryptorLh, encrypt, true, "AES-CBC encrypt", ctx.Logger);

                    Func<ICipher> decryptorNet = () => Aes.CreateCbcDecryptor(new byte[0x10], new byte[0x10], true);
                    Func<ICipher> decryptorLh = () => Aes.CreateCbcDecryptor(new byte[0x10], new byte[0x10]);
                    CipherTaskSeparate decrypt = (input, output, key1, key2, iv, crypto) =>
                        Aes.DecryptCbc128(input, output, key1, iv, crypto);

                    RunCipherBenchmark(decryptorNet, decryptorLh, decrypt, true, "AES-CBC decrypt", ctx.Logger);

                    break;
                }

                case "aesctrnew":
                {
                    Func<ICipher> encryptorNet = () => Aes.CreateCtrEncryptor(new byte[0x10], new byte[0x10], true);
                    Func<ICipher> encryptorLh = () => Aes.CreateCtrEncryptor(new byte[0x10], new byte[0x10]);
                    CipherTaskSeparate encrypt = (input, output, key1, key2, iv, crypto) =>
                        Aes.EncryptCtr128(input, output, key1, iv, crypto);

                    RunCipherBenchmark(encryptorNet, encryptorLh, encrypt, true, "AES-CTR", ctx.Logger);

                    break;
                }
                case "aesxtsnew":
                {
                    Func<ICipher> encryptorNet = () => Aes.CreateXtsEncryptor(new byte[0x10], new byte[0x10], new byte[0x10], true);
                    Func<ICipher> encryptorLh = () => Aes.CreateXtsEncryptor(new byte[0x10], new byte[0x10], new byte[0x10]);
                    CipherTaskSeparate encrypt = (input, output, key1, key2, iv, crypto) =>
                        Aes.EncryptXts128(input, output, key1, key2, iv, crypto);

                    RunCipherBenchmark(encryptorNet, encryptorLh, encrypt, false, "AES-XTS encrypt", ctx.Logger);

                    Func<ICipher> decryptorNet = () => Aes.CreateXtsDecryptor(new byte[0x10], new byte[0x10], new byte[0x10], true);
                    Func<ICipher> decryptorLh = () => Aes.CreateXtsDecryptor(new byte[0x10], new byte[0x10], new byte[0x10]);
                    CipherTaskSeparate decrypt = (input, output, key1, key2, iv, crypto) =>
                        Aes.DecryptXts128(input, output, key1, key2, iv, crypto);

                    RunCipherBenchmark(decryptorNet, decryptorLh, decrypt, false, "AES-XTS decrypt", ctx.Logger);

                    break;
                }

                default:
                    ctx.Logger.LogMessage("Unknown benchmark type.");
                    return;
            }
        }
    }
}
