using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibHac.Tests
{
    public class FileSystemTests
    {
        public static void Main(string[] args)
        {
            LocalFileSystem fs = new LocalFileSystem("G:\\\\");
            foreach(IFile file in fs.RootDirectory.Files)
            {
                Console.WriteLine(file.Path);
            }

            foreach (IDirectory directory in fs.RootDirectory.Directories)
            {
                Console.WriteLine(directory.Path);
            }

            string baseDir = "C:\\Users\\Somebody Whoisbored\\.switch\\";
            Keyset keyset = ExternalKeys.ReadKeyFile(baseDir + "prod.keys", baseDir + "title.keys", baseDir + "console.keys");

            SwitchFs sw = new SwitchFs(keyset, fs);
            ;

            Console.ReadKey();
        }
    }
}
