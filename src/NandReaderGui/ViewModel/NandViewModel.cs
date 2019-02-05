using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using LibHac;
using LibHac.IO;
using LibHac.IO.Save;
using LibHac.Nand;

namespace NandReaderGui.ViewModel
{
    public class NandViewModel : ViewModelBase
    {
        public List<DiskInfo> Disks { get; } = new List<DiskInfo>();
        public ICommand OpenCommand { get; set; }
        public DiskInfo SelectedDisk { get; set; }

        public NandViewModel()
        {
            OpenCommand = new RelayCommand(Open);

            var query = new WqlObjectQuery("SELECT * FROM Win32_DiskDrive");
            using (var searcher = new ManagementObjectSearcher(query))
            {
                foreach (ManagementBaseObject drive in searcher.Get())
                {
                    if (drive.GetPropertyValue("Size") == null) continue;
                    var info = new DiskInfo();
                    info.PhysicalName = (string)drive.GetPropertyValue("Name");
                    info.Name = (string)drive.GetPropertyValue("Caption");
                    info.Model = (string)drive.GetPropertyValue("Model");
                    //todo Why is Windows returning small sizes? https://stackoverflow.com/questions/15051660
                    info.Length = (long)((ulong)drive.GetPropertyValue("Size"));
                    info.SectorSize = (int)((uint)drive.GetPropertyValue("BytesPerSector"));
                    info.DisplaySize = Util.GetBytesReadable((long)((ulong)drive.GetPropertyValue("Size")));

                    Disks.Add(info);
                }
            }
        }

        public void Open()
        {
            DiskInfo disk = SelectedDisk;
            var storage = new CachedStorage(new DeviceStream(disk.PhysicalName, disk.Length).AsStorage(), disk.SectorSize * 100, 4, true);
            Stream stream = storage.AsStream(FileAccess.Read);

            Keyset keyset = OpenKeyset();
            var nand = new Nand(stream, keyset);

            Stream prodinfo = nand.OpenProdInfo();
            var calibration = new Calibration(prodinfo);

            keyset.EticketExtKeyRsa = Crypto.DecryptRsaKey(calibration.EticketExtKeyRsa, keyset.EticketRsaKek);
            Ticket[] tickets = GetTickets(keyset, nand);

            using (var outStream = new StreamWriter("titlekeys.txt"))
            {
                foreach (Ticket ticket in tickets)
                {
                    byte[] key = ticket.GetTitleKey(keyset);
                    outStream.WriteLine($"{ticket.RightsId.ToHexString()},{key.ToHexString()}");
                }
            }
        }

        private static Ticket[] GetTickets(Keyset keyset, Nand nand, IProgressReport logger = null)
        {
            var tickets = new List<Ticket>();
            FatFileSystemProvider system = nand.OpenSystemPartition();

            IFile saveE1File = system.OpenFile("/save/80000000000000E1", OpenMode.Read);
            tickets.AddRange(ReadTickets(keyset, saveE1File.AsStream()));

            IFile saveE2 = system.OpenFile("/save/80000000000000E2", OpenMode.Read);
            tickets.AddRange(ReadTickets(keyset, saveE2.AsStream()));

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

    public class DiskInfo
    {
        public string PhysicalName { get; set; }
        public string Name { get; set; }
        public string Model { get; set; }
        public long Length { get; set; }
        public int SectorSize { get; set; }
        public string DisplaySize { get; set; }
        public string Display => $"{Name} ({DisplaySize})";
    }
}
