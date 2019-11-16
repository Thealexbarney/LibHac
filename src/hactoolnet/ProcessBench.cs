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

            int blockCount = src.Length / 0x10;

            for (int i = 0; i < iterations; i++)
            {
                ICipher cipher = cipherGenerator();

                watch.Restart();

                for (int b = 0; b < blockCount; b++)
                {
                    cipher.Transform(src.Slice(b * 0x10, 0x10), dst.Slice(b * 0x10, 0x10));
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

        private static void RunCipherBenchmark(Func<ICipher> cipherNet, Func<ICipher> cipherLibHac, bool benchBlocked,
            string label, IProgressReport logger)
        {
            var srcData = new byte[Size];

            var dstDataLh = new byte[Size];
            var dstDataNet = new byte[Size];
            var dstDataBlockedLh = new byte[Size];
            var dstDataBlockedNet = new byte[Size];

            logger.LogMessage(string.Empty);
            logger.LogMessage(label);

            if (AesCrypto.IsAesNiSupported()) CipherBenchmark(srcData, dstDataLh, cipherLibHac, Iterations, "LibHac impl: ", logger);
            CipherBenchmark(srcData, dstDataNet, cipherNet, Iterations, ".NET impl:   ", logger);

            if (benchBlocked)
            {
                if (AesCrypto.IsAesNiSupported())
                    CipherBenchmarkBlocked(srcData, dstDataBlockedLh, cipherLibHac, Iterations / 5, "LibHac impl (blocked): ", logger);

                CipherBenchmarkBlocked(srcData, dstDataBlockedNet, cipherNet, Iterations / 5, ".NET impl (blocked):   ", logger);
            }

            if (AesCrypto.IsAesNiSupported())
            {
                logger.LogMessage($"{dstDataLh.SequenceEqual(dstDataNet)}");

                if (benchBlocked)
                {
                    logger.LogMessage($"{dstDataLh.SequenceEqual(dstDataBlockedLh)}");
                    logger.LogMessage($"{dstDataLh.SequenceEqual(dstDataBlockedNet)}");
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
                    Func<ICipher> encryptorNet = () => AesCrypto.CreateEcbEncryptor(new byte[0x10], true);
                    Func<ICipher> encryptorLh = () => AesCrypto.CreateEcbEncryptor(new byte[0x10]);

                    RunCipherBenchmark(encryptorNet, encryptorLh, true, "AES-ECB encrypt", ctx.Logger);

                    Func<ICipher> decryptorNet = () => AesCrypto.CreateEcbDecryptor(new byte[0x10], true);
                    Func<ICipher> decryptorLh = () => AesCrypto.CreateEcbDecryptor(new byte[0x10]);

                    RunCipherBenchmark(decryptorNet, decryptorLh, true, "AES-ECB decrypt", ctx.Logger);

                    break;
                }
                case "aescbcnew":
                {
                    Func<ICipher> encryptorNet = () => AesCrypto.CreateCbcEncryptor(new byte[0x10], new byte[0x10], true);
                    Func<ICipher> encryptorLh = () => AesCrypto.CreateCbcEncryptor(new byte[0x10], new byte[0x10]);

                    RunCipherBenchmark(encryptorNet, encryptorLh, true, "AES-CBC encrypt", ctx.Logger);

                    Func<ICipher> decryptorNet = () => AesCrypto.CreateCbcDecryptor(new byte[0x10], new byte[0x10], true);
                    Func<ICipher> decryptorLh = () => AesCrypto.CreateCbcDecryptor(new byte[0x10], new byte[0x10]);

                    RunCipherBenchmark(decryptorNet, decryptorLh, true, "AES-CBC decrypt", ctx.Logger);

                    break;
                }

                case "aesctrnew":
                {
                    Func<ICipher> encryptorNet = () => AesCrypto.CreateCtrEncryptor(new byte[0x10], new byte[0x10], true);
                    Func<ICipher> encryptorLh = () => AesCrypto.CreateCtrEncryptor(new byte[0x10], new byte[0x10]);

                    RunCipherBenchmark(encryptorNet, encryptorLh, true, "AES-CTR", ctx.Logger);

                    break;
                }
                case "aesxtsnew":
                {
                    Func<ICipher> encryptorNet = () => AesCrypto.CreateXtsEncryptor(new byte[0x10], new byte[0x10], new byte[0x10], true);
                    Func<ICipher> encryptorLh = () => AesCrypto.CreateXtsEncryptor(new byte[0x10], new byte[0x10], new byte[0x10]);

                    RunCipherBenchmark(encryptorNet, encryptorLh, false, "AES-XTS encrypt", ctx.Logger);

                    Func<ICipher> decryptorNet = () => AesCrypto.CreateXtsDecryptor(new byte[0x10], new byte[0x10], new byte[0x10], true);
                    Func<ICipher> decryptorLh = () => AesCrypto.CreateXtsDecryptor(new byte[0x10], new byte[0x10], new byte[0x10]);

                    RunCipherBenchmark(decryptorNet, decryptorLh, false, "AES-XTS decrypt", ctx.Logger);

                    break;
                }

                default:
                    ctx.Logger.LogMessage("Unknown benchmark type.");
                    return;
            }
        }
    }
}
