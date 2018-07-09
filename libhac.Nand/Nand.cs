using System;
using System.IO;
using System.Linq;
using DiscUtils;
using DiscUtils.Fat;
using DiscUtils.Partitions;
using DiscUtils.Streams;
using libhac.XTSSharp;
using Directory = System.IO.Directory;

namespace libhac.Nand
{
    public class Nand
    {
        public Nand(Stream stream, Keyset keyset, IProgressReport logger)
        {
            var disc = new GuidPartitionTable(stream, Geometry.Null);
            var partitions = disc.Partitions.Select(x => (GuidPartitionInfo) x).ToArray();
            var sys = partitions.FirstOrDefault(x => x.Name == "SYSTEM");
            var user = partitions.FirstOrDefault(x => x.Name == "USER");
            var sysStream = sys.Open();
            var xts = XTSSharp.XtsAes128.Create(keyset.bis_keys[2]);
            var decStream = new RandomAccessSectorStream(new XtsSectorStream(sysStream, xts, 0x4000, 0), true);

            FatFileSystem fat = new FatFileSystem(decStream, Ownership.None);
            var dirs = fat.GetDirectories("Contents", "*", SearchOption.AllDirectories);
            var files = fat.GetFiles("save");
            var f = fat.OpenFile("save\\80000000000000E2", FileMode.Open, FileAccess.Read);
            var save = new byte[f.Length];
            f.Read(save, 0, save.Length);

            Directory.CreateDirectory("tickets");
            var ticket = new byte[0x400];
            // brute force it
            for (int i = 0; i < save.Length - 16; i += 16)
            {
                if (save[i] != 0x52 || save[i + 1] != 0x6f || save[i + 2] != 0x6f || save[i + 3] != 0x74)
                    continue;

                Array.Copy(save, i - 0x140, ticket, 0, 0x400);
                var titleid = BitConverter.ToString(ticket, 0x2a0, 16).Replace("-", string.Empty);
                File.WriteAllBytes($"tickets/{titleid}.tik", ticket);
            }


            ;
            var s = fat.FileExists("save\\80000000000000E1");

            ;
            using (var output = new FileStream("80000000000000E1", FileMode.Create, FileAccess.ReadWrite))
            {
                f.CopyStream(output, f.Length, logger);
            }
                ;
        }
    }
}
