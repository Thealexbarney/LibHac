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
            LocalFileSystem fs = new LocalFileSystem("C:\\\\");
            foreach(IFile file in fs.RootDirectory.Files)
            {
                Console.WriteLine(file.Path);
            }

            foreach (IDirectory directory in fs.RootDirectory.Directories)
            {
                Console.WriteLine(directory.Path);
            }

            Console.ReadKey();
        }
    }
}
