// ReSharper disable UnusedVariable UnusedMember.Local
using System;
using System.Collections.Generic;
using System.IO;
using LibHac;
using LibHac.IO;
using LibHac.IO.Save;
using LibHac.Nand;

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
                Keyset keyset = OpenKeyset();
                var nand = new Nand(stream, keyset);
                Stream prodinfo = nand.OpenProdInfo();
                var calibration = new Calibration(prodinfo);

                keyset.EticketExtKeyRsa = Crypto.DecryptRsaKey(calibration.EticketExtKeyRsa, keyset.EticketRsaKek);
                Ticket[] tickets = GetTickets(keyset, nand, logger);

                foreach (Ticket ticket in tickets)
                {
                    byte[] key = ticket.GetTitleKey(keyset);
                    logger.LogMessage($"{ticket.RightsId.ToHexString()},{key.ToHexString()}");
                }
            }
        }

        private static void ReadSwitchFs(string nandFile)
        {
            using (var logger = new ProgressBar())
            using (var stream = new FileStream(nandFile, FileMode.Open, FileAccess.Read))
            {
                Keyset keyset = OpenKeyset();
                var nand = new Nand(stream, keyset);
                NandPartition user = nand.OpenSystemPartition();
                var sdfs = new SwitchFs(keyset, user);
            }
        }

        private static void ReadCalibration(string nandFile)
        {
            using (var logger = new ProgressBar())
            using (var stream = new FileStream(nandFile, FileMode.Open, FileAccess.Read))
            {
                Keyset keyset = OpenKeyset();
                var nand = new Nand(stream, keyset);
                Stream prodinfo = nand.OpenProdInfo();
                var calibration = new Calibration(prodinfo);
            }
        }

        private static void DumpTickets(string nandFile)
        {
            using (var logger = new ProgressBar())
            using (var stream = new FileStream(nandFile, FileMode.Open, FileAccess.Read))
            {
                Keyset keyset = OpenKeyset();
                var nand = new Nand(stream, keyset);
                Ticket[] tickets = GetTickets(keyset, nand, logger);

                Directory.CreateDirectory("tickets");
                foreach (Ticket ticket in tickets)
                {
                    string filename = Path.Combine("tickets", $"{ticket.RightsId.ToHexString()}.tik");
                    File.WriteAllBytes(filename, ticket.File);
                }
            }
        }

        private static Ticket[] GetTickets(Keyset keyset, Nand nand, IProgressReport logger = null)
        {
            var tickets = new List<Ticket>();
            NandPartition system = nand.OpenSystemPartition();

            Stream saveE1File = system.OpenFile("save\\80000000000000E1", FileMode.Open, FileAccess.Read);
            tickets.AddRange(ReadTickets(keyset, saveE1File));

            Stream saveE2 = system.OpenFile("save\\80000000000000E2", FileMode.Open, FileAccess.Read);
            tickets.AddRange(ReadTickets(keyset, saveE2));

            logger?.LogMessage($"Found {tickets.Count} tickets");

            return tickets.ToArray();
        }

        private static List<Ticket> ReadTickets(Keyset keyset, Stream savefile)
        {
            var tickets = new List<Ticket>();
            var save = new SaveDataFileSystem(keyset, savefile.AsStorage(), IntegrityCheckLevel.None, true);
            var ticketList = new BinaryReader(save.OpenFile("/ticket_list.bin", OpenMode.Read).AsStream());
            var ticketFile = new BinaryReader(save.OpenFile("/ticket.bin", OpenMode.Read).AsStream());

            ulong titleId = ticketList.ReadUInt64();
            while (titleId != ulong.MaxValue)
            {
                ticketList.BaseStream.Position += 0x18;
                long start = ticketFile.BaseStream.Position;
                tickets.Add(new Ticket(ticketFile));
                ticketFile.BaseStream.Position = start + 0x400;
                titleId = ticketList.ReadUInt64();
            }

            return tickets;
        }

        private static Keyset OpenKeyset()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string homeKeyFile = Path.Combine(home, ".switch", "prod.keys");
            string homeTitleKeyFile = Path.Combine(home, ".switch", "title.keys");
            string homeConsoleKeyFile = Path.Combine(home, ".switch", "console.keys");
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

