using System.IO;

namespace Main
{
    public class DirectoryEnumerationContext
    {
        private DirectoryEnumerationContext()
        {

        }
        public bool GetNextFileInfo(out FileSystemInfo info)
        {
            bool Result = false;
            info = null;
            if (mIndex < mFileList.Length)
            {
                info = mFileList[mIndex];
                ++mIndex;
                info.Refresh();
                Result = info.Exists;
            }
            return Result;
        }

        public DirectoryEnumerationContext(string DirName, string Mask)
        {
            DirectoryInfo dirinfo = new DirectoryInfo(DirName);

            mFileList = dirinfo.GetFileSystemInfos(Mask);

            mIndex = 0;
        }
        private FileSystemInfo[] mFileList;
        private int mIndex;
    }
}
