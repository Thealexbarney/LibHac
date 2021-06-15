# LibHac

[![NuGet](https://img.shields.io/nuget/v/LibHac.svg?style=flat-square)](https://www.nuget.org/packages/LibHac)
[![MyGet](https://img.shields.io/myget/libhac/vpre/libhac.svg?label=myget&style=flat-square)](https://www.myget.org/feed/libhac/package/nuget/LibHac)
[![AppVeyor Build Status](https://img.shields.io/appveyor/ci/thealexbarney/LibHac/master.svg?style=flat-square)](https://ci.appveyor.com/project/Thealexbarney/libhac/history)

LibHac is a .NET library that reimplements some parts of the Nintendo Switch operating system, also known as Horizon OS.

One of the other main functions of the library is opening, decrypting and extracting common content file formats used by Horizon.

Most content is imported and exported using a standard `IStorage` interface. This means that reading nested file types and encryptions can easily be done by linking different file readers together.  
For example, the files from a title stored on the external SD card can be read or extracted in this way.  
`NAX0 Reader` -> `NCA Reader` -> `RomFS Reader` -> `Individual Files`

## Getting Started

[Handling Content Files](docs/getting-started.md)

Todo: Document usage of the `Horizon` and `HorizonClient` objects.

# hactoolnet

hactoolnet is an example program that uses LibHac. It is used in a similar manner to [hactool](https://github.com/SciresM/hactool).

## Usage
```
Usage: hactoolnet.exe [options...] <path>
Options:
  -r, --raw            Keep raw data, don't unpack.
  -y, --verify         Verify all hashes in the input file.
  -h, --enablehash     Enable hash checks when reading the input file.
  -d, --dev            Decrypt with development keys instead of retail.
  -k, --keyset         Load keys from an external file.
  -t, --intype=type    Specify input file type [nca, xci, romfs, pfs0, pk11, pk21, ini1, kip1, switchfs, save, ndv0, keygen, romfsbuild, pfsbuild]
  --titlekeys <file>   Load title keys from an external file.
NCA options:
  --plaintext <file>   Specify file path for saving a decrypted copy of the NCA.
  --header <file>      Specify Header file path.
  --section0 <file>    Specify Section 0 file path.
  --section1 <file>    Specify Section 1 file path.
  --section2 <file>    Specify Section 2 file path.
  --section3 <file>    Specify Section 3 file path.
  --section0dir <dir>  Specify Section 0 directory path.
  --section1dir <dir>  Specify Section 1 directory path.
  --section2dir <dir>  Specify Section 2 directory path.
  --section3dir <dir>  Specify Section 3 directory path.
  --exefs <file>       Specify ExeFS file path.
  --exefsdir <dir>     Specify ExeFS directory path.
  --romfs <file>       Specify RomFS file path.
  --romfsdir <dir>     Specify RomFS directory path.
  --listromfs          List files in RomFS.
  --basenca            Set Base NCA to use with update partitions.
RomFS options:
  --romfsdir <dir>     Specify RomFS directory path.
  --listromfs          List files in RomFS.
RomFS creation options:
                       Input path must be a directory
  --outfile <file>     Specify created RomFS file path.
Partition FS options:
  --outdir <dir>       Specify extracted FS directory path.
Partition FS creation options:
                       Input path must be a directory
  --outfile <file>     Specify created Partition FS file path.
  --hashedfs           Create a hashed Partition FS (HFS0).
XCI options:
  --rootdir <dir>      Specify root XCI directory path.
  --updatedir <dir>    Specify update XCI directory path.
  --normaldir <dir>    Specify normal XCI directory path.
  --securedir <dir>    Specify secure XCI directory path.
  --logodir <dir>      Specify logo XCI directory path.
  --outdir <dir>       Specify XCI directory path.
  --exefs <file>       Specify main ExeFS file path.
  --exefsdir <dir>     Specify main ExeFS directory path.
  --romfs <file>       Specify main RomFS file path.
  --romfsdir <dir>     Specify main RomFS directory path.
  --nspout <file>      Specify file for the created NSP.
Package1 options:
  --outdir <dir>       Specify Package1 directory path.
Package2 options:
  --outdir <dir>       Specify Package2 directory path.
INI1 options:
  --outdir <dir>       Specify INI1 directory path.
Switch FS options:
  --sdseed <seed>      Set console unique seed for SD card NAX0 encryption.
  --listapps           List application info.
  --listtitles         List title info for all titles.
  --listncas           List info for all NCAs.
  --title <title id>   Specify title ID to use.
  --outdir <dir>       Specify directory path to save title NCAs to. (--title must be specified)
  --exefs <file>       Specify ExeFS directory path. (--title must be specified)
  --exefsdir <dir>     Specify ExeFS directory path. (--title must be specified)
  --romfs <file>       Specify RomFS directory path. (--title must be specified)
  --romfsdir <dir>     Specify RomFS directory path. (--title must be specified)
  --savedir <dir>      Specify save file directory path.
  -y, --verify         Verify all titles, or verify a single title if --title is set.
Save data options:
  --outdir <dir>       Specify directory path to save contents to.
  --debugoutdir <dir>  Specify directory path to save intermediate data to for debugging.
  --sign               Sign the save file. (Requires device_key in key file)
  --trim               Trim garbage data in the save file. (Requires device_key in key file)
  --listfiles          List files in save file.
  --repack <dir>       Replaces the contents of the save data with the specified directory.
  --replacefile <filename in save> <file> Replaces a file in the save data
NDV0 (Delta) options:
                       Input delta patch can be a delta NCA file or a delta fragment file.
  --basefile <file>    Specify base file path.
  --outfile            Specify patched file path.
Keygen options:
  --outdir <dir>       Specify directory path to save key files to.
```

## Examples

#### List applications on a Switch SD card or NAND
`hactoolnet -t switchfs --sdseed <sd_seed> --listapps <sd_root_path>`

#### Extract a title from an SD card or NAND as NCA files
`hactoolnet -t switchfs --sdseed <sd_seed> --title <title_id> --outdir output <sd_root_path>`

#### Extract the RomFS from a title from an SD card or NAND
`hactoolnet -t switchfs --sdseed <sd_seed> --title <title_id> --romfsdir romfs <sd_root_path>`

Specifying the base title ID will extract the unpatched title.  
Specifying the patch title ID will extract the patched title.

## External Keys

For more detailed information on keyset files, see [KEYS.md](KEYS.md).

Keys can be loaded from a text file by specifying a filename with the `-k` argument. The file should be in the same format read by [hactool](https://github.com/SciresM/hactool#external-keys):  
"Keyset files are text files containing one key per line, in the form "key_name = HEXADECIMALKEY". Case shouldn't matter, nor should whitespace."

Console-unique keys can be loaded from a text file by specifying a filename with the `--consolekeys` argument. The file format is the same as the main keyset file.

Title keys can be loaded from a text file by specifying a filename with the `--titlekeys` argument. The file should contain one key per line in the form `rights_id,HEXADECIMALKEY`.

If a keyfile is not set at the command line, hactoolnet will search for and load keyfiles in `$HOME/.switch/prod.keys`, `$HOME/.switch/console.keys` and `$HOME/.switch/title.keys`.

## Special Thanks

This project uses NDepend for static code analysis.

[![NDepend link](img/NDependLogo.png)](https://www.ndepend.com/)