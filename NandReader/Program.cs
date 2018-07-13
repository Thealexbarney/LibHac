using System;
using System.Collections.Generic;
using System.IO;
using libhac;
using libhac.Nand;

namespace NandReader
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Usage: NandReader raw_nand_dump_file");
                return;
            }
            GetTitleKeys(args[0]);
        }

        private static void GetTitleKeys(string nandFile)
        {
            using (var logger = new ProgressBar())
            using (var stream = new FileStream(nandFile, FileMode.Open, FileAccess.Read))
            {
                var keyset = OpenKeyset();
                var nand = new Nand(stream, keyset);
                var prodinfo = nand.OpenProdInfo();
                var calibration = new Calibration(prodinfo);

                keyset.eticket_ext_key_rsa = Crypto.DecryptRsaKey(calibration.EticketExtKeyRsa, keyset.eticket_rsa_kek);
                var tickets = GetTickets(nand, logger);

                foreach (var ticket in tickets)
                {
                    var key = ticket.GetTitleKey(keyset);
                    logger.LogMessage($"{ticket.RightsId.ToHexString()},{key.ToHexString()}");
                }
            }
        }

        private static void ReadSwitchFs(string nandFile)
        {
            using (var logger = new ProgressBar())
            using (var stream = new FileStream(nandFile, FileMode.Open, FileAccess.Read))
            {
                var keyset = OpenKeyset();
                var nand = new Nand(stream, keyset);
                var user = nand.OpenSystemPartition();
                var sdfs = new SdFs(keyset, user);
            }
        }

        private static void ReadCalibration(string nandFile)
        {
            using (var logger = new ProgressBar())
            using (var stream = new FileStream(nandFile, FileMode.Open, FileAccess.Read))
            {
                var keyset = OpenKeyset();
                var nand = new Nand(stream, keyset);
                var prodinfo = nand.OpenProdInfo();
                var calibration = new Calibration(prodinfo);
            }
        }

        private static void DumpTickets(string nandFile)
        {
            using (var logger = new ProgressBar())
            using (var stream = new FileStream(nandFile, FileMode.Open, FileAccess.Read))
            {
                var keyset = OpenKeyset();
                var nand = new Nand(stream, keyset);
                var tickets = GetTickets(nand, logger);

                Directory.CreateDirectory("tickets");
                foreach (var ticket in tickets)
                {
                    var filename = Path.Combine("tickets", $"{ticket.RightsId.ToHexString()}.tik");
                    File.WriteAllBytes(filename, ticket.File);
                }
            }
        }

        private static Ticket[] GetTickets(Nand nand, IProgressReport logger = null)
        {
            var tickets = new List<Ticket>();
            var system = nand.OpenSystemPartition();

            logger?.LogMessage("Searching save 80000000000000E1");
            var saveE1 = system.OpenFile("save\\80000000000000E1", FileMode.Open, FileAccess.Read);
            tickets.AddRange(Ticket.SearchTickets(saveE1, logger));

            logger?.LogMessage("Searching save 80000000000000E2");
            var saveE2 = system.OpenFile("save\\80000000000000E2", FileMode.Open, FileAccess.Read);
            tickets.AddRange(Ticket.SearchTickets(saveE2, logger));

            logger?.LogMessage($"Found {tickets.Count} tickets");

            return tickets.ToArray();
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

