using System.Diagnostics;
using LibHac;
using LibHac.IO;

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

            string rate = Util.GetBytesReadable((long)(src.GetSize() * iterations / encryptWatch.Elapsed.TotalSeconds));
            logger.LogMessage($"{label}{rate}/s");
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
                default:
                    ctx.Logger.LogMessage("Unknown benchmark type.");
                    return;
            }
        }
    }
}
