using System.IO;
using CallbackFS;

namespace Main
{
    static class FileSystem
    {
        internal static class Virtual
        {
            private const string Root = @"\";

            public static bool IsRoot(string fileName)
            {
                return Root.Equals(fileName);
            }

            public static bool IsDirectory(uint fileAttributes)
            {
                return (fileAttributes & (uint)CbFsFileAttributes.CBFS_FILE_ATTRIBUTE_DIRECTORY) != 0;
            }
        }

        internal static class Real
        {
            private const string Root = "C:\\1";

            public static string GetFileName(string fileName)
            {
                return fileName == "\\" ? Root : Root + fileName;
            }

            public static bool IsDirectory(FileAttributes fileAttributes)
            {
                return (fileAttributes & FileAttributes.Directory) != 0;
            }
        }
    }
}
