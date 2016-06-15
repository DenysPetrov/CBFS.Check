using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using CallbackFS;
using log4net;

namespace Main
{
    public class CbfsHandler : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private const string CbfsProductName = "713CC6CE-B3E2-4fd9-838D-E28F558F6866";

        private static readonly string CbfsLicenseCode =
            "90FF5FAA64D5C07862C7E4F9460B88FDDAF65477AEC10109F17E834" +
            "0B2F65CDF5D7CB09747081C1F5CD40168F3106DF9299BE83CFF25B2" +
            "E18B664F43727778047FB92F0E93D0056EF33065E68B2C01365B7CD1" +
            "62E720D582C7C075660BAC8182C7C07572773065D2B754E95EC4";

        private const uint MountingFlags = CallbackFileSystem.CBFS_SYMLINK_SIMPLE | CallbackFileSystem.CBFS_SYMLINK_LOCAL;

        private readonly CallbackFileSystem _cbFs;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetLogicalDrives();

        public CbfsHandler()
        {
            _cbFs = new CallbackFileSystem
            {
                OnMount = CbFsMount,
                OnUnmount = CbFsUnmount,
                OnGetVolumeSize = CbFsGetVolumeSize,
                OnGetVolumeLabel = CbFsGetVolumeLabel,
                OnGetVolumeId = CbFsGetVolumeId,
                OnCreateFile = CbFsCreateFile,
                OnOpenFile = CbFsOpenFile,
                OnCloseFile = CbFsCloseFile,
                OnGetFileInfo = CbFsGetFileInfo,
                OnEnumerateDirectory = CbFsEnumerateDirectory,
                OnCloseDirectoryEnumeration = CbFsCloseDirectoryEnumeration,
                OnCloseNamedStreamsEnumeration = CbFsCloseNamedStreamsEnumeration,
                OnSetEndOfFile = CbFsSetEndOfFile,
                OnSetFileAttributes = CbFsSetFileAttributes,
                OnCanFileBeDeleted = CbFsCanFileBeDeleted,
                OnDeleteFile = CbFsDeleteFile,
                OnRenameOrMoveFile = CbFsRenameOrMoveFile,
                OnReadFile = CbFsReadFile,
                OnWriteFile = CbFsWriteFile,
                OnIsDirectoryEmpty = CbFsIsDirectoryEmpty
            };
        }

        public string MountingPoint { get; private set; }

        public void Initialize()
        {
            CreateStorage();

            CreateMountingPoint();

            try
            {
                _cbFs.MountMedia(10000);
            }
            catch (ECBFSError err)
            {
                Console.WriteLine(err.Message);
            }

            _cbFs.MetaDataCacheEnabled = false;
            _cbFs.FileCacheEnabled = false;
        }

        private void CbFsMount(object sender)
        {
        }

        private void CbFsUnmount(object sender)
        {

        }

        private string FindAvailableDriveLetter()
        {
            int drives = GetLogicalDrives();

            char driveletter = 'z';
            while ((1 << driveletter - 'a' & drives) > 0)
                driveletter--;

            return $"{driveletter}:";
        }
        
        private void CreateStorage()
        {
            CallbackFileSystem.SetRegistrationKey(CbfsLicenseCode);

            _cbFs.MaxWorkerThreadCount = (uint)Environment.ProcessorCount * 2;
            _cbFs.StorageCharacteristics = 0;

            // Emulate CBFS 3 behavior
            // TODO: split global fileContexts into fileContexts/handleContexts, a feature in CBFS 5
            _cbFs.CallAllOpenCloseCallbacks = false;
            _cbFs.ChangeTimeAttributeSupported = false;

            _cbFs.CreateStorage();
        }

        private void CreateMountingPoint()
        {
            MountingPoint = FindAvailableDriveLetter();

            _cbFs.AddMountingPoint(MountingPoint, MountingFlags, null); //TODO: check CallbackFileSystem.CBFS_SYMLINK_MOUNT_MANAGER
        }

        private bool InstallDriver()
        {
            // must be run with Admin rigths
            try
            {
                uint reboot = 0;
                CallbackFileSystem.Install(AppDomain.CurrentDomain.BaseDirectory + @"\driver\cbfs.cab",
                    CbfsProductName, string.Empty, false,
                    CallbackFileSystem.CBFS_MODULE_DRIVER |
                    CallbackFileSystem.CBFS_MODULE_MOUNT_NOTIFIER_DLL |
                    CallbackFileSystem.CBFS_MODULE_NET_REDIRECTOR_DLL,
                    ref reboot);
                return reboot != 0;
            }
            catch (ECBFSError err)
            {
                Console.WriteLine("Unable to install driver: {0}", err);
                throw;
            }
        }

        public void Dispose()
        {
            DeleteMountingPoint();

            DeleteStorage();
        }

        private void DeleteMountingPoint()
        {
            if (_cbFs.Active)
            {
                try
                {
                    _cbFs.DeleteMountingPoint(MountingPoint, MountingFlags, null);
                }
                catch (Exception ex)
                {
                    Log.Error($"Failed to delete mouting point {MountingPoint}", ex);
                }

                try
                {
                    _cbFs.UnmountMedia(true);
                }
                catch (Exception ex)
                {
                    Log.Error("Failed to unmout media", ex);
                }
            }
        }

        private void DeleteStorage()
        {
            if (_cbFs.StoragePresent)
            {
                try
                {
                    _cbFs.DeleteStorage(true);
                }
                catch (Exception ex)
                {
                    Log.Error("Failed to delete storage", ex);
                }
            }
        }

        private void CbFsGetVolumeSize(
            object sender,
            ref long totalNumberOfSectors,
            ref long numberOfFreeSectors
            )
        {
            try
            {
                var inf = new DriveInfo(FileSystem.Real.GetFileName(string.Empty));
                long freeSpace = inf.AvailableFreeSpace;
                long totalSpace = inf.TotalSize;

                numberOfFreeSectors = freeSpace / _cbFs.SectorSize;
                totalNumberOfSectors = totalSpace / _cbFs.SectorSize;
            }
            catch (Exception ex)
            {
                Log.Error("GetVolumeSize - unhandled exception", ex);
                throw;
            }
        }

        private void CbFsGetVolumeLabel(object sender, ref string volumeLabel)
        {
            volumeLabel = "CoreData test";
        }

        private void CbFsCloseFile(CallbackFileSystem sender, CbFsFileInfo fileInfo, CbFsHandleInfo handleInfo)
        {
            if (!fileInfo.UserContext.IsZero())
            {
                var fileStreamContext = fileInfo.UserContext.To<FileStreamContext>();

                if (fileStreamContext.Stream.SafeFileHandle.IsInvalid)
                {
                    throw new IOException("Could not open file stream.", new Win32Exception());
                }
                if (fileStreamContext.OpenCount == 1)
                {
                    fileStreamContext.Stream?.Close();
                    fileInfo.DeallocateUserContext();
                }
                else
                {
                    fileStreamContext.DecrementCounter();
                }
            }
        }
        
        private static void CbFsGetVolumeId(object sender, ref uint volumeId)
        {
            volumeId = 0x12345678;
        }

        private void CbFsCreateFile(CallbackFileSystem sender, string fileName, uint desiredAccess,
            uint fileAttributes, uint shareMode, CbFsFileInfo fileInfo, CbFsHandleInfo handleInfo)
        {
            var fileStreamContext = new FileStreamContext();

            var fullFileName = FileSystem.Real.GetFileName(fileName);

            if (FileSystem.Virtual.IsDirectory(fileAttributes))
            {
                Directory.CreateDirectory(fullFileName);
            }
            else
            {
                fileStreamContext.Stream = new FileStream(fullFileName, FileMode.Create, FileAccess.ReadWrite,
                    FileShare.ReadWrite | FileShare.Delete);
            }

            fileInfo.UserContext = IntPtrExtensions.Allocate(fileStreamContext);
            fileStreamContext.IncrementCounter();
        }

        private void CbFsOpenFile(CallbackFileSystem sender, string fileName, uint desiredAccess,
            uint fileAttributes, uint shareMode, CbFsFileInfo fileInfo, CbFsHandleInfo handleInfo)
        {
            FileStreamContext fileStreamContext;

            if (!fileInfo.UserContext.IsZero())
            {
                fileStreamContext = fileInfo.UserContext.To<FileStreamContext>();
                fileStreamContext.IncrementCounter();
                return;
            }
            
            fileStreamContext = new FileStreamContext();

            if (FileSystem.Virtual.IsDirectory(fileAttributes))
            {
                var fullFileName = FileSystem.Real.GetFileName(fileName);
                fileStreamContext.Stream = new FileStream(fullFileName, FileMode.Open, FileAccess.ReadWrite,
                    FileShare.ReadWrite | FileShare.Delete);
            }

            fileStreamContext.IncrementCounter();
            fileInfo.UserContext = IntPtrExtensions.Allocate(fileStreamContext);
        }

        private void CbFsGetFileInfo(
             CallbackFileSystem sender,
             string fileName,
             ref bool fileExists,
             ref DateTime creationTime,
             ref DateTime lastAccessTime,
             ref DateTime lastWriteTime,
             ref DateTime changeTime,
             ref long endOfFile,
             ref long allocationSize,
             ref long fileId,
             ref uint fileAttributes,
             ref uint numberOfLinks,
             ref string shortFileName,
             ref string realFileName
        )
        {
            var fullFileName = FileSystem.Real.GetFileName(fileName);
            fileExists = false;

            try
            {
                var attributes = File.GetAttributes(fullFileName);
                fileAttributes = Convert.ToUInt32(attributes);
            }
            catch (Exception ex)
            {
                Log.Error($"File not found: {fullFileName}", ex);
                return; // file not found
            }

            creationTime = File.GetCreationTimeUtc(fullFileName);
            lastAccessTime = File.GetLastAccessTimeUtc(fullFileName);
            lastWriteTime = File.GetLastWriteTimeUtc(fullFileName);

            if (FileSystem.Virtual.IsDirectory(fileAttributes))
            {
                endOfFile = 0;
            }
            else
            {
                var info = new FileInfo(fullFileName);
                endOfFile = info.Length;
            }

            allocationSize = endOfFile;
            numberOfLinks = 1;
            fileId = 0;
            fileExists = true;
        }

        private void CbFsEnumerateDirectory(
            CallbackFileSystem sender,
            CbFsFileInfo directoryInfo,
            CbFsHandleInfo handleInfo,
            CbFsDirectoryEnumerationInfo directoryEnumerationInfo,
            string mask,
            int index,
            bool restart,
            ref bool fileFound,
            ref string fileName,
            ref string shortFileName,
            ref DateTime creationTime,
            ref DateTime lastAccessTime,
            ref DateTime lastWriteTime,
            ref DateTime changeTime,
            ref long endOfFile,
            ref long allocationSize,
            ref long fileId,
            ref uint fileAttributes
            )
        {
            DirectoryEnumerationContext context;

            if (restart && !directoryEnumerationInfo.UserContext.IsZero())
            {
                directoryEnumerationInfo.DeallocateUserContext();
            }
            if (directoryEnumerationInfo.UserContext.IsZero())
            {
                var fullFileName = FileSystem.Real.GetFileName(directoryInfo.FileName);
                context = new DirectoryEnumerationContext(fullFileName, mask);
                directoryEnumerationInfo.UserContext = IntPtrExtensions.Allocate(context);
            }
            else
            {
                context = directoryEnumerationInfo.UserContext.To<DirectoryEnumerationContext>();
            }

            FileSystemInfo finfo;
            while (fileFound = context.GetNextFileInfo(out finfo))
            {
                if (finfo.Name != "." && finfo.Name != "..") break;
            }

            if (fileFound)
            {
                fileName = finfo.Name;
                creationTime = finfo.CreationTime;
                lastAccessTime = finfo.LastAccessTime;
                lastWriteTime = finfo.LastWriteTime;
                endOfFile = FileSystem.Real.IsDirectory(finfo.Attributes) ? 0 : ((FileInfo)finfo).Length;
                allocationSize = endOfFile;
                fileAttributes = Convert.ToUInt32(finfo.Attributes);
                fileId = 0;
            }
            else
            {
                directoryEnumerationInfo.DeallocateUserContext();
            }
        }

        private void CbFsCloseDirectoryEnumeration(CallbackFileSystem sender, CbFsFileInfo directoryInfo, CbFsDirectoryEnumerationInfo directoryEnumerationInfo)
        {
            directoryEnumerationInfo.UserContext.Deallocate();
        }

        private void CbFsCloseNamedStreamsEnumeration(object sender, CbFsFileInfo directoryInfo,
            CbFsNamedStreamsEnumerationInfo enumerationInfo)
        {
            if (!enumerationInfo.UserContext.IsZero())
            {
                var ctx = enumerationInfo.UserContext.To<AlternateDataStreamContext>();
                ctx.hFile.Close();
                enumerationInfo.UserContext.Deallocate();
            }
        }

        private void CbFsSetEndOfFile(CallbackFileSystem sender, CbFsFileInfo fileInfo, long endOfFile)
        {
            var fileStreamContext = fileInfo.UserContext.To<FileStreamContext>();
            var fileStream = fileStreamContext.Stream;

            if (fileStream.SafeFileHandle.IsInvalid)
            {
                throw new IOException("Could not open file stream.", new Win32Exception());
            }

            fileStream.Position = endOfFile;
            fileStream.SetLength(endOfFile);
            fileStream.Flush();
        }

        private void CbFsSetFileAttributes(
            CallbackFileSystem sender,
            CbFsFileInfo fileInfo,
            CbFsHandleInfo handleInfo,
            DateTime creationTime,
            DateTime lastAccessTime,
            DateTime lastWriteTime,
            DateTime changeTime,
            uint attributes
            )
        {
            var fullFileName = FileSystem.Real.GetFileName(fileInfo.FileName);
            var fileAttributes = (FileAttributes) attributes;
            var isDirectory = FileSystem.Real.IsDirectory(fileAttributes);

            if (attributes != 0)
            {
                File.SetAttributes(fullFileName, fileAttributes);
            }

            Action<DateTime, Action<string, DateTime>, Action<string, DateTime>> setTimeAttribute =
                (time, fileSetAction, dirSetAction) =>
                {
                    if (time != DateTime.MinValue)
                    {
                        if (isDirectory)
                            dirSetAction(fullFileName, time);
                        else
                            fileSetAction(fullFileName, time);
                    }
                };
            setTimeAttribute(creationTime, File.SetCreationTimeUtc, Directory.SetCreationTimeUtc);
            setTimeAttribute(lastAccessTime, File.SetLastAccessTimeUtc, Directory.SetLastAccessTimeUtc);
            setTimeAttribute(lastWriteTime, File.SetLastWriteTimeUtc, Directory.SetLastWriteTimeUtc);
        }

        private void CbFsCanFileBeDeleted(CallbackFileSystem sender, CbFsFileInfo fileInfo, CbFsHandleInfo handleInfo, ref bool canBeDeleted)
        {
            canBeDeleted = true;
        }

        private void CbFsDeleteFile(object sender, CbFsFileInfo fileInfo)
        {
            var fullFileName = FileSystem.Real.GetFileName(fileInfo.FileName);
            var fileAttributes = File.GetAttributes(fullFileName);

            var info = FileSystem.Real.IsDirectory(fileAttributes)
                ? (FileSystemInfo) new DirectoryInfo(fullFileName)
                : new FileInfo(fullFileName);

            info.Delete();
        }

        private void CbFsRenameOrMoveFile(object sender, CbFsFileInfo fileInfo, string newFileName)
        {
            var currentFullFileName = FileSystem.Real.GetFileName(fileInfo.FileName);
            var newFullFileName = FileSystem.Real.GetFileName(newFileName);
            var fileAttributes = File.GetAttributes(currentFullFileName);

            if (FileSystem.Real.IsDirectory(fileAttributes))
            {
                var dirInfo = new DirectoryInfo(currentFullFileName);
                dirInfo.MoveTo(newFullFileName);
            }
            else
            {
                var currentFileInfo = new FileInfo(currentFullFileName);
                var newFileInfo = new FileInfo(newFullFileName);
                if (newFileInfo.Exists)
                {
                    newFileInfo.Delete();
                }
                currentFileInfo.MoveTo(newFullFileName);
            }
        }

        private void CbFsReadFile(
            CallbackFileSystem sender,
            CbFsFileInfo fileInfo,
            long position,
            byte[] buffer,
            int bytesToRead,
            ref int bytesRead
            )
        {
            var fileStreamContext = fileInfo.UserContext.To<FileStreamContext>();
            var fileStream = fileStreamContext.Stream;

            if (fileStream.SafeFileHandle.IsInvalid)
            {
                throw new IOException("Could not open file stream.", new Win32Exception());
            }
            fileStream.Seek(position, SeekOrigin.Begin);

            bytesRead = fileStream.Read(buffer, 0, bytesToRead);

            fileStream.Flush();
        }

        private void CbFsWriteFile(
            CallbackFileSystem sender,
            CbFsFileInfo fileInfo,
            long position,
            byte[] buffer,
            int bytesToWrite,
            ref int bytesWritten)
        {
            bytesWritten = 0;

            var fileStreamContext = fileInfo.UserContext.To<FileStreamContext>();
            var fileStream = fileStreamContext.Stream;

            if (fileStream.SafeFileHandle.IsInvalid)
            {
                throw new IOException("Could not open file stream.", new Win32Exception());
            }
            fileStream.Seek(position, SeekOrigin.Begin);

            var currentPosition = fileStream.Position;
            fileStream.Write(buffer, 0, bytesToWrite);
            bytesWritten = (int)(fileStream.Position - currentPosition);
            fileStream.Flush();
        }

        private void CbFsIsDirectoryEmpty(CallbackFileSystem sender, CbFsFileInfo directoryInfo, string fileName, ref bool isEmpty)
        {
            var fullFileName = FileSystem.Real.GetFileName(fileName);
            var dir = new DirectoryInfo(fullFileName);
            var fileInfos = dir.GetFileSystemInfos();
            isEmpty = fileInfos.Length == 0;
        }
    }
}
