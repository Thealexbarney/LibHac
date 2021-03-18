using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using LibHac;
using LibHac.Crypto;
using LibHac.Crypto.Impl;
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

        private const int BatchCipherBenchSize = 1024 * 1024;
        // ReSharper disable once UnusedMember.Local
        private const int SingleBlockCipherBenchSize = 1024 * 128;
        private const int ShaBenchSize = 1024 * 128;

        private static double CpuFrequency { get; set; }

        private static void CopyBenchmark(IStorage src, IStorage dst, int iterations, string label, IProgressReport logger)
        {
            // Warmup
            src.CopyTo(dst);

            logger.SetTotal(iterations);

            var encryptWatch = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                src.CopyTo(dst);
                logger.ReportAdd(1);
            }
            encryptWatch.Stop();
            logger.SetTotal(0);

            src.GetSize(out long srcSize).ThrowIfFailure();

            string rate = Utilities.GetBytesReadable((long)(srcSize * iterations / encryptWatch.Elapsed.TotalSeconds));
            logger.LogMessage($"{label}{rate}/s");
        }

        private static void CipherBenchmark(ReadOnlySpan<byte> src, Span<byte> dst, Func<ICipher> cipherGenerator,
            int iterations, string label, IProgressReport logger)
        {
            cipherGenerator().Transform(src, dst);

            var watch = new Stopwatch();
            double[] runTimes = new double[iterations];

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

            string fastestRate = Utilities.GetBytesReadable((long)(srcSize / fastestRun));
            string averageRate = Utilities.GetBytesReadable((long)(srcSize / averageRun));
            string slowestRate = Utilities.GetBytesReadable((long)(srcSize / slowestRun));

            logger.LogMessage($"{label}{averageRate}/s, fastest run: {fastestRate}/s, slowest run: {slowestRate}/s");
        }

        private static void CipherBenchmarkBlocked(ReadOnlySpan<byte> src, Span<byte> dst, Func<ICipher> cipherGenerator,
            int iterations, string label, IProgressReport logger)
        {
            cipherGenerator().Transform(src, dst);

            var watch = new Stopwatch();
            double[] runTimes = new double[iterations];

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

            string fastestRate = Utilities.GetBytesReadable((long)(srcSize / fastestRun));
            string averageRate = Utilities.GetBytesReadable((long)(srcSize / averageRun));
            string slowestRate = Utilities.GetBytesReadable((long)(srcSize / slowestRun));

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
            double[] runTimes = new double[iterations];

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

            string fastestRate = Utilities.GetBytesReadable((long)(srcSize / fastestRun));
            string averageRate = Utilities.GetBytesReadable((long)(srcSize / averageRun));
            string slowestRate = Utilities.GetBytesReadable((long)(srcSize / slowestRun));

            logger.LogMessage($"{label}{averageRate}/s, fastest run: {fastestRate}/s, slowest run: {slowestRate}/s");
        }

        private static void RegisterAesSequentialBenchmarks(MultiBenchmark bench)
        {
            byte[] input = new byte[BatchCipherBenchSize];
            byte[] output = new byte[BatchCipherBenchSize];

            Func<double, string> resultPrinter = time => GetPerformanceString(time, BatchCipherBenchSize);

            // Skip the first benchmark set if we don't have AES-NI intrinsics
            for (int i = Aes.IsAesNiSupported() ? 0 : 1; i < 2; i++)
            {
                // Prefer .NET crypto on the second set
                string nameSuffix = i == 1 ? "built-in " : string.Empty;
                bool preferDotNetImpl = i == 1;

                RegisterCipher($"AES-ECB {nameSuffix}encrypt",
                    () => Aes.CreateEcbEncryptor(new byte[0x10], preferDotNetImpl));

                RegisterCipher($"AES-ECB {nameSuffix}decrypt",
                    () => Aes.CreateEcbDecryptor(new byte[0x10], preferDotNetImpl));

                RegisterCipher($"AES-CBC {nameSuffix}encrypt",
                    () => Aes.CreateCbcEncryptor(new byte[0x10], new byte[0x10], preferDotNetImpl));

                RegisterCipher($"AES-CBC {nameSuffix}decrypt",
                    () => Aes.CreateCbcDecryptor(new byte[0x10], new byte[0x10], preferDotNetImpl));

                RegisterCipher($"AES-CTR {nameSuffix}decrypt",
                    () => Aes.CreateCtrDecryptor(new byte[0x10], new byte[0x10], preferDotNetImpl));

                RegisterCipher($"AES-XTS {nameSuffix}encrypt",
                    () => Aes.CreateXtsEncryptor(new byte[0x10], new byte[0x10], new byte[0x10], preferDotNetImpl));

                RegisterCipher($"AES-XTS {nameSuffix}decrypt",
                    () => Aes.CreateXtsDecryptor(new byte[0x10], new byte[0x10], new byte[0x10], preferDotNetImpl));
            }

            void RegisterCipher(string name, Func<ICipher> cipherGenerator)
            {
                ICipher cipher = null;

                Action setup = () => cipher = cipherGenerator();
                Action action = () => cipher.Transform(input, output);

                bench.Register(name, setup, action, resultPrinter);
            }
        }

        // ReSharper disable once UnusedParameter.Local
        private static void RegisterAesSingleBlockBenchmarks(MultiBenchmark bench)
        {
            byte[] input = new byte[SingleBlockCipherBenchSize];
            byte[] output = new byte[SingleBlockCipherBenchSize];

            Func<double, string> resultPrinter = time => GetPerformanceString(time, SingleBlockCipherBenchSize);

            bench.Register("AES single-block encrypt", () => { }, EncryptBlocks, resultPrinter);
            bench.Register("AES single-block decrypt", () => { }, DecryptBlocks, resultPrinter);

            bench.DefaultRunsNeeded = 1000;

            void EncryptBlocks()
            {
                ref byte inBlock = ref MemoryMarshal.GetReference(input.AsSpan());
                ref byte outBlock = ref MemoryMarshal.GetReference(output.AsSpan());

                Vector128<byte> keyVec = Vector128<byte>.Zero;

                ref byte end = ref Unsafe.Add(ref inBlock, input.Length);

                while (Unsafe.IsAddressLessThan(ref inBlock, ref end))
                {
                    var inputVec = Unsafe.ReadUnaligned<Vector128<byte>>(ref inBlock);
                    Vector128<byte> outputVec = AesCoreNi.EncryptBlock(inputVec, keyVec);
                    Unsafe.WriteUnaligned(ref outBlock, outputVec);

                    inBlock = ref Unsafe.Add(ref inBlock, Aes.BlockSize);
                    outBlock = ref Unsafe.Add(ref outBlock, Aes.BlockSize);
                }
            }

            void DecryptBlocks()
            {
                ref byte inBlock = ref MemoryMarshal.GetReference(input.AsSpan());
                ref byte outBlock = ref MemoryMarshal.GetReference(output.AsSpan());

                Vector128<byte> keyVec = Vector128<byte>.Zero;

                ref byte end = ref Unsafe.Add(ref inBlock, input.Length);

                while (Unsafe.IsAddressLessThan(ref inBlock, ref end))
                {
                    var inputVec = Unsafe.ReadUnaligned<Vector128<byte>>(ref inBlock);
                    Vector128<byte> outputVec = AesCoreNi.DecryptBlock(inputVec, keyVec);
                    Unsafe.WriteUnaligned(ref outBlock, outputVec);

                    inBlock = ref Unsafe.Add(ref inBlock, Aes.BlockSize);
                    outBlock = ref Unsafe.Add(ref outBlock, Aes.BlockSize);
                }
            }
        }

        private static void RegisterShaBenchmarks(MultiBenchmark bench)
        {
            byte[] input = new byte[ShaBenchSize];
            byte[] digest = new byte[Sha256.DigestSize];

            Func<double, string> resultPrinter = time => GetPerformanceString(time, ShaBenchSize);

            RegisterHash("SHA-256 built-in", () => new Sha256Generator());

            void RegisterHash(string name, Func<IHash> hashGenerator)
            {
                IHash hash = null;

                Action setup = () =>
                {
                    hash = hashGenerator();
                    hash.Initialize();
                };

                Action action = () =>
                {
                    hash.Update(input);
                    hash.GetHash(digest);
                };

                bench.Register(name, setup, action, resultPrinter);
            }
        }

        private static void RunCipherBenchmark(Func<ICipher> cipherNet, Func<ICipher> cipherLibHac,
            CipherTaskSeparate function, bool benchBlocked, string label, IProgressReport logger)
        {
            byte[] srcData = new byte[Size];

            byte[] dstDataLh = new byte[Size];
            byte[] dstDataNet = new byte[Size];
            byte[] dstDataBlockedLh = new byte[Size];
            byte[] dstDataBlockedNet = new byte[Size];
            byte[] dstDataSeparateLh = new byte[Size];
            byte[] dstDataSeparateNet = new byte[Size];

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

        private static string GetPerformanceString(double seconds, long bytes)
        {
            string cyclesPerByteString = string.Empty;
            double bytesPerSec = bytes / seconds;

            if (CpuFrequency > 0)
            {
                double cyclesPerByte = CpuFrequency / bytesPerSec;
                cyclesPerByteString = $" ({cyclesPerByte:N3}x)";
            }

            return Utilities.GetBytesReadable((long)bytesPerSec) + "/s" + cyclesPerByteString;
        }

        public static void Process(Context ctx)
        {
            CpuFrequency = ctx.Options.CpuFrequencyGhz * 1_000_000_000;

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
                    CipherTaskSeparate encrypt = (input, output, key1, _, _, crypto) =>
                        Aes.EncryptEcb128(input, output, key1, crypto);

                    RunCipherBenchmark(encryptorNet, encryptorLh, encrypt, true, "AES-ECB encrypt", ctx.Logger);

                    Func<ICipher> decryptorNet = () => Aes.CreateEcbDecryptor(new byte[0x10], true);
                    Func<ICipher> decryptorLh = () => Aes.CreateEcbDecryptor(new byte[0x10]);
                    CipherTaskSeparate decrypt = (input, output, key1, _, _, crypto) =>
                        Aes.DecryptEcb128(input, output, key1, crypto);

                    RunCipherBenchmark(decryptorNet, decryptorLh, decrypt, true, "AES-ECB decrypt", ctx.Logger);

                    break;
                }

                case "aescbcnew":
                {
                    Func<ICipher> encryptorNet = () => Aes.CreateCbcEncryptor(new byte[0x10], new byte[0x10], true);
                    Func<ICipher> encryptorLh = () => Aes.CreateCbcEncryptor(new byte[0x10], new byte[0x10]);
                    CipherTaskSeparate encrypt = (input, output, key1, _, iv, crypto) =>
                        Aes.EncryptCbc128(input, output, key1, iv, crypto);

                    RunCipherBenchmark(encryptorNet, encryptorLh, encrypt, true, "AES-CBC encrypt", ctx.Logger);

                    Func<ICipher> decryptorNet = () => Aes.CreateCbcDecryptor(new byte[0x10], new byte[0x10], true);
                    Func<ICipher> decryptorLh = () => Aes.CreateCbcDecryptor(new byte[0x10], new byte[0x10]);
                    CipherTaskSeparate decrypt = (input, output, key1, _, iv, crypto) =>
                        Aes.DecryptCbc128(input, output, key1, iv, crypto);

                    RunCipherBenchmark(decryptorNet, decryptorLh, decrypt, true, "AES-CBC decrypt", ctx.Logger);

                    break;
                }

                case "aesctrnew":
                {
                    Func<ICipher> encryptorNet = () => Aes.CreateCtrEncryptor(new byte[0x10], new byte[0x10], true);
                    Func<ICipher> encryptorLh = () => Aes.CreateCtrEncryptor(new byte[0x10], new byte[0x10]);
                    CipherTaskSeparate encrypt = (input, output, key1, _, iv, crypto) =>
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

                case "crypto":
                {
                    var bench = new MultiBenchmark();

                    RegisterAesSequentialBenchmarks(bench);
                    RegisterAesSingleBlockBenchmarks(bench);
                    RegisterShaBenchmarks(bench);

                    bench.Run();
                    break;
                }

                default:
                    ctx.Logger.LogMessage("Unknown benchmark type.");
                    return;
            }
        }
    }
}
