using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using libhac;

namespace hactoolnet
{
    public static class Program
    {
        static void Main(string[] args)
        {
            DumpMeta(args);
        }

        static void DecryptNax0(string[] args)
        {
            var keyset = ExternalKeys.ReadKeyFile(args[0]);
            keyset.SetSdSeed(args[1].ToBytes());

            var nax0 = new Nax0(keyset, args[2], args[3]);
            var nca = new Nca(keyset, nax0.Stream);

            using (var output = new FileStream(args[4], FileMode.Create))
            using (var progress = new ProgressBar())
            {
                progress.LogMessage($"Title ID: {nca.Header.TitleId:X16}");
                progress.LogMessage($"Writing {args[4]}");
                nax0.Stream.CopyStream(output, nax0.Stream.Length, progress);
            }
        }

        static void ListSdContents(string[] args)
        {
            Console.WriteLine($"Using key file {args[0]}");
            Console.WriteLine($"SD seed {BitConverter.ToString(args[1].ToBytes())}");
            Console.WriteLine($"SD path {args[2]}");
            var keyset = ExternalKeys.ReadKeyFile(args[0]);

            if (keyset.master_keys[0].IsEmpty())
            {
                Console.WriteLine("Need master key 0");
            }

            keyset.SetSdSeed(args[1].ToBytes());
            var sdfs = new SdFs(keyset, args[2]);
            var ncas = sdfs.ReadAllNca();

            foreach (var nca in ncas.Where(x => x != null))
            {
                Console.WriteLine($"{nca.Header.TitleId:X16} {nca.Header.ContentType.ToString().PadRight(10, ' ')} {nca.Name}");
            }
        }

        static void DumpMeta(string[] args)
        {
            var keyset = ExternalKeys.ReadKeyFile(args[0]);
            keyset.SetSdSeed(args[1].ToBytes());
            var sdfs = new SdFs(keyset, args[2]);
            var ncas = sdfs.ReadAllNca().ToArray();

            var metadata = new List<byte[]>();

            using (var progress = new ProgressBar())
            {
                foreach (var nca in ncas.Where(x => x.Header.ContentType == ContentType.Meta))
                {
                    foreach (var section in nca.Sections.Where(x => x.Header.FsType == SectionFsType.Pfs0))
                    {
                        var sect = nca.OpenSection(section.SectionNum);
                        var pfs0 = sect.Pfs0;
                        pfs0.Open(sect.Stream);

                        foreach (var entry in pfs0.Entries)
                        {
                            var path = Path.Combine("meta", entry.Name);
                            Directory.CreateDirectory(Path.GetDirectoryName(path));
                            var file = pfs0.GetFile(entry.Index);
                            metadata.Add(file);
                            File.WriteAllBytes(path, file);
                            progress.LogMessage(path);
                        }
                    }
                }
            }
        }
    }
}
