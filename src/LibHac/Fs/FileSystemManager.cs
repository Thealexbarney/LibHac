using LibHac.Fs.Accessors;

namespace LibHac.Fs
{
    public class FileSystemManager
    {
        internal Horizon Os { get; }

        internal MountTable MountTable { get; } = new MountTable();

        public FileSystemManager(Horizon os)
        {
            Os = os;
        }
    }
}
