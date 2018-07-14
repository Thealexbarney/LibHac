using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using libhac;
using libhac.Nand;
using libhac.XTSSharp;

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
                foreach (var drive in searcher.Get())
                {
                    if (drive.GetPropertyValue("Size") == null) continue;
                    var info = new DiskInfo();
                    info.PhysicalName = (string)drive.GetPropertyValue("Name");
                    info.Name = (string)drive.GetPropertyValue("Caption");
                    info.Model = (string)drive.GetPropertyValue("Model");
                    info.Length = (long)((ulong)drive.GetPropertyValue("Size"));
                    info.SectorSize = (int)((uint)drive.GetPropertyValue("BytesPerSector"));
                    info.DisplaySize = Util.GetBytesReadable((long)((ulong)drive.GetPropertyValue("Size")));

                    Disks.Add(info);
                }
            }
        }

        public void Open()
        {
            var disk = SelectedDisk;
            var stream = new RandomAccessSectorStream(new SectorStream(new DeviceStream(disk.PhysicalName, disk.Length), disk.SectorSize * 100));

            var keyset = OpenKeyset();
            var nand = new Nand(stream, keyset);

            var prodinfo = nand.OpenProdInfo();
            var calibration = new Calibration(prodinfo);

            keyset.eticket_ext_key_rsa = Crypto.DecryptRsaKey(calibration.EticketExtKeyRsa, keyset.eticket_rsa_kek);
            var tickets = GetTickets(nand);

            using (var outStream = new StreamWriter("titlekeys.txt"))
            {
                foreach (var ticket in tickets)
                {
                    var key = ticket.GetTitleKey(keyset);
                    outStream.WriteLine($"{ticket.RightsId.ToHexString()},{key.ToHexString()}");
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
