namespace LibHac.FsSystem
{
    internal static class Messages
    {
        public static string DestSpanTooSmall => "Destination array is not long enough to hold the requested data.";
        public static string NcaSectionMissing => "NCA section does not exist.";
        public static string DestPathIsSubPath => "The destination directory is a subdirectory of the source directory.";
        public static string DestPathAlreadyExists => "Destination path already exists.";
        public static string PartialPathNotFound => "Could not find a part of the path.";
    }
}
