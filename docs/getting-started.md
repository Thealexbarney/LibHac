# Getting Started

## LibHac Interfaces

LibHac uses several interfaces for reading and writing data and files. These are based off the interfaces used by Horizon OS.

### IStorage

`IStorage` is an interface that LibHac uses for reading and writing data.
An `IStorage` is similar to a .NET `Stream`, but an `IStorage` does not keep track of its position. An offset must be provided on every read.

`IStorage` uses byte Spans for reading and writing data. The length of the data read or written is equal to the length of the provided span.

### IFile

`IFile` is similar to `IStorage` with slight differences. 
- `IFile` can automatically grow if data is written past the end of the file. `IStorage` does not grow by default.
- When more bytes are requested than there are bytes available,
`IFile` will read as many bytes as it can and return the number of bytes read. `IStorage` will throw an exception.

### IFileSystem

This is an interface for representing a standard file system. It provides functionality for reading files, navigating the file system, creating files, etc. 

## Using LibHac

### Loading Keys

Most of LibHac's functionality requires a `Keyset` object that holds encryption keys required for reading content.

This can be done by loading keys from an external text file, or by creating a new `Keyset` and copying the keys into it.
```
Keyset keyset = ExternalKeys.ReadKeyFile("common_key_file", "title_key_file", "console_key_file");
```

The text files should follow the format as specified [here](../KEYS.md).

### Reading an NCA

Open an NCA and get an `IStorage` of the decrypted file.
```
using (IStorage inFile = new LocalStorage("filename.nca", FileAccess.Read))
{
    var nca = new Nca(keyset, inFile);

    IStorage decryptedNca = nca.OpenDecryptedNca();
}
```

Open an NCA's code section.
```
using (IStorage inFile = new LocalStorage("filename.nca", FileAccess.Read))
{
    var nca = new Nca(keyset, inFile);

    IStorage section = nca.OpenStorage(NcaSectionType.Code, IntegrityCheckLevel.ErrorOnInvalid);
}
```

Open an NCA's data section as an `IFileSystem`.
```
using (IStorage inFile = new LocalStorage("filename.nca", FileAccess.Read))
{
    var nca = new Nca(keyset, inFile);

    IFileSystem fileSystem = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None);
}
```

Extension methods are provided for common operations on LibHac interfaces.

`IFileSystem.CopyFileSystem` will fully copy the contents of one `IFileSystem` to another.
```
using (IStorage inFile = new LocalStorage("filename.nca", FileAccess.Read))
{
    var nca = new Nca(keyset, inFile);
    var outFileSystem = new LocalFileSystem("extracted_path");

    IFileSystem fileSystem = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.ErrorOnInvalid);
    fileSystem.CopyFileSystem(outFileSystem);
}
```

Open a patched NCA.
```
using (IStorage baseFile = new LocalStorage("base.nca", FileAccess.Read))
using (IStorage patchFile = new LocalStorage("base.nca", FileAccess.Read))
{
    var baseNca = new Nca(keyset, baseFile);
    var patchNca = new Nca(keyset, patchFile);

    IFileSystem fileSystem = baseNca.OpenFileSystemWithPatch(patchNca, NcaSectionType.Data,
        IntegrityCheckLevel.ErrorOnInvalid);
}
```

### IFileSystem Operations

Open a file and read the first 0x4000 bytes.
```
using (IStorage inFile = new LocalStorage("filename.nca", FileAccess.Read))
{
    var nca = new Nca(keyset, inFile);
    IFileSystem fileSystem = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.ErrorOnInvalid);

    var buffer = new byte[0x4000];

    IFile myFile = fileSystem.OpenFile("/my/file/path.ext", OpenMode.Read);
    int bytesRead = myFile.Read(buffer, 0);
}
```

An `IDirectory` can be used to enumerate the file system entries in a directory.

...