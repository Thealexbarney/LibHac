using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace hactoolnet
{
    internal class MultiBenchmark
    {
        public int RunsNeeded { get; set; } = 500;

        private List<BenchmarkItem> Benchmarks { get; } = new List<BenchmarkItem>();

        public void Register(string name, Action setupAction, Action runAction, Func<double, string> resultPrinter)
        {
            var benchmark = new BenchmarkItem
            {
                Name = name,
                Setup = setupAction,
                Run = runAction,
                PrintResult = resultPrinter
            };

            Benchmarks.Add(benchmark);
        }

        public void Run()
        {
            foreach (BenchmarkItem item in Benchmarks)
            {
                RunBenchmark(item);

                Console.WriteLine($"{item.Name}: {item.Result}");
            }
        }

        private void RunBenchmark(BenchmarkItem item)
        {
            double fastestRun = double.MaxValue;
            var watch = new Stopwatch();

            int runsSinceLastBest = 0;

            while (runsSinceLastBest < RunsNeeded)
            {
                runsSinceLastBest++;
                item.Setup();

                watch.Restart();
                item.Run();
                watch.Stop();

                if (fastestRun > watch.Elapsed.TotalSeconds)
                {
                    fastestRun = watch.Elapsed.TotalSeconds;

                    runsSinceLastBest = 0;
                }
            }

            item.Time = fastestRun;
            item.Result = item.PrintResult(item.Time);
        }

        private class BenchmarkItem
        {
            public string Name { get; set; }
            public double Time { get; set; }
            public string Result { get; set; }

            public Action Setup { get; set; }
            public Action Run { get; set; }
            public Func<double, string> PrintResult { get; set; }
        }
    }
}
