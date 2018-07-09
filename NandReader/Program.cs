using System;
using System.IO;
using libhac;
using libhac.Nand;

namespace NandReader
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            DumpTickets();
        }

        private static void DumpTickets()
        {
            using (var logger = new ProgressBar())
            using (var stream = new FileStream(@"F:\rawnand.bin", FileMode.Open, FileAccess.Read))
            {
                var keyset = OpenKeyset();
                var nand = new Nand(stream, keyset, logger);
            }
        }

        private static Keyset OpenKeyset()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var homeKeyFile = Path.Combine(home, ".switch", "prod.keys");
            var homeTitleKeyFile = Path.Combine(home, ".switch", "title.keys");
            var homeConsoleKeyFile = Path.Combine(home, ".switch", "console.keys");
            string keyFile = null;
            string titleKeyFile = null;
            string consoleKeyFile = null;

            if (File.Exists(homeKeyFile))
            {
                keyFile = homeKeyFile;
            }

            if (File.Exists(homeTitleKeyFile))
            {
                titleKeyFile = homeTitleKeyFile;
            }

            if (File.Exists(homeConsoleKeyFile))
            {
                consoleKeyFile = homeConsoleKeyFile;
            }

            return ExternalKeys.ReadKeyFile(keyFile, titleKeyFile, consoleKeyFile);
        }
    }
}
