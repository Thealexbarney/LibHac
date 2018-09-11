# LibHac

LibHac is a .NET or .NET Core library for opening, decrypting and extracting common content file formats used by the Nintendo Switch.

All content is imported and exported as .NET streams. This means that reading nested file types and encryptions can easily be done by linking different file readers together.  
For example, the files from a title stored on the external SD card can be read or extracted in this way.  
`NAX0 Reader` -> `NCA Reader` -> `RomFS Reader` -> `Individual Files`

# hactoolnet

hactoolnet is an example program that uses LibHac. It is used in a similar manner to [hactool](https://github.com/SciresM/hactool).

## Usage
```
Usage: hactoolnet.exe [options...] <path>
Options:
  -r, --raw            Keep raw data, don't unpack.
  -y, --verify         Verify hashes.
  -k, --keyset         Load keys from an external file.
  -t, --intype=type    Specify input file type [nca, xci, switchfs, save, keygen]
  --titlekeys <file>   Load title keys from an external file.
NCA options:
  --section0 <file>    Specify Section 0 file path.
  --section1 <file>    Specify Section 1 file path.
  --section2 <file>    Specify Section 2 file path.
  --section3 <file>    Specify Section 3 file path.
  --section0dir <dir>  Specify Section 0 directory path.
  --section1dir <dir>  Specify Section 1 directory path.
  --section2dir <dir>  Specify Section 2 directory path.
  --section3dir <dir>  Specify Section 3 directory path.
  --exefs <file>       Specify ExeFS directory path.
  --exefsdir <dir>     Specify ExeFS directory path.
  --romfs <file>       Specify RomFS directory path.
  --romfsdir <dir>     Specify RomFS directory path.
  --listromfs          List files in RomFS.
  --basenca            Set Base NCA to use with update partitions.
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
Switch FS options:
  --sdseed <seed>      Set console unique seed for SD card NAX0 encryption.
  --listapps           List application info.
  --listtitles         List title info for all titles.
  --title <title id>   Specify title ID to use.
  --outdir <dir>       Specify directory path to save title NCAs to. (--title must be specified)
  --exefs <file>       Specify ExeFS directory path. (--title must be specified)
  --exefsdir <dir>     Specify ExeFS directory path. (--title must be specified)
  --romfs <file>       Specify RomFS directory path. (--title must be specified)
  --romfsdir <dir>     Specify RomFS directory path. (--title must be specified)
  --savedir <dir>      Specify save file directory path.
Savefile options:
  --outdir <dir>       Specify directory path to save contents to.
  --debugoutdir <dir>  Specify directory path to save intermediate data to for debugging.
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