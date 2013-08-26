using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Timers;
using System.Runtime.InteropServices;

namespace FileWatcher
{
    public class MainProgram
    {
        private List<FileSystemWatcher> listFsw;
        private DateTime lastUpdate = DateTime.MinValue;
        private bool update = false;
        private Timer timer;
        private Timer moveTimer;
        private Dictionary<string, List<DirectoryInfo>> dirInfo;
        private string[] topFolders;
        private string shareFolder;
        private Queue<string> moveQueue;
        private int timerInterval = 10000;
        private int moveTimerInterval = 60000;
        private string logFile = "log.txt";
        private int consoleLogLevel = 3;
        private int fileLogLevel = 4;
        private List<string> foundTempDirs;

        /*
         * 0: Critical
         * 1: Error
         * 2: Warning
         * 3: Information
         * 4: Debug
         */ 
        public void Log(int logLevel, string message)
        {
            using (StreamWriter tw = File.AppendText(logFile))
            {
                string tmp = string.Format(" {0}> ", DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss"));
                switch (logLevel)
                {
                    case 0:
                        if (logLevel <= consoleLogLevel)
                        {
                            Console.WriteLine("[CRITICAL]" + tmp + message);
                        }
                        if (logLevel <= fileLogLevel)
                        {
                            tw.WriteLine("[CRITICAL]" + tmp + message);
                        }
                        break;
                    case 1:
                        if (logLevel <= consoleLogLevel)
                        {
                            Console.WriteLine("[ERROR]   " + tmp + message);
                        }
                        if (logLevel <= fileLogLevel)
                        {
                            tw.WriteLine("[ERROR]   " + tmp + message);
                        }
                        break;
                    case 2:
                        if (logLevel <= consoleLogLevel)
                        {
                            Console.WriteLine("[WARN]    " + tmp + message);
                        }
                        if (logLevel <= fileLogLevel)
                        {
                            tw.WriteLine("[WARN]    " + tmp + message);
                        }
                        break;
                    case 3:
                        if (logLevel <= consoleLogLevel)
                        {
                            Console.WriteLine("[INFO]    " + tmp + message);
                        }
                        if (logLevel <= fileLogLevel)
                        {
                            tw.WriteLine("[INFO]    " + tmp + message);
                        }
                        break;
                    case 4:
                        if (logLevel <= consoleLogLevel)
                        {
                            Console.WriteLine("[DEBUG]   " + tmp + message);
                        }
                        if (logLevel <= fileLogLevel)
                        {
                            tw.WriteLine("[DEBUG]   " + tmp + message);
                        }
                        break;
                    default:
                        throw new Exception("Unknown log level:" + logLevel);
                        break;
                }
            }
        }

        public MainProgram()
        {
            moveQueue = new Queue<string>();
            listFsw = new List<FileSystemWatcher>();
            dirInfo = new Dictionary<string, List<DirectoryInfo>>();
            timer = new Timer();
            timer.Interval = 1000;
            timer.Elapsed += timer_Elapsed;
            timer.AutoReset = false;
            timer.Start();

            moveTimer = new Timer();
            moveTimer.Interval = 1000;
            moveTimer.Elapsed += moveTimer_Elapsed;
            moveTimer.AutoReset = false;
            moveTimer.Start();

            topFolders = File.ReadAllLines("ListOfDirectories.txt");
            shareFolder = File.ReadAllLines("ShareFolder.txt")[0];
        }

        private void moveTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            int minimumFreeSpace = 1024;
            
            AddToMoveQueue(GetNewFolders(shareFolder));

            string item = null;
            try
            {
                Log(4, "Dequeing item");
                item = moveQueue.Dequeue();
                Log(4, string.Format("Got item {0}", item));
            } catch { }

            while (item != null)
            {
                long itemSize = GetDirectorySize(item);
                Log(4, string.Format("Item is {0} bytes", itemSize));
                DirectoryInfo dirInfo = FindPathWithMostFreeSpace(topFolders);
                Log(4, string.Format("Best drive is {0}", dirInfo.Root.Name));

                long sizeAfterItemMb = (new DriveInfo(dirInfo.Root.Name).AvailableFreeSpace - itemSize) / 1024 / 1024;

                if (sizeAfterItemMb > minimumFreeSpace)
                {
                    string newPath = item.Replace(shareFolder, dirInfo.FullName);
                    bool dirAlreadyExists = false;
                    string tempPath = newPath + "_temp";
                    DirectoryInfo tempDir = null;

                    Log(3, string.Format("Processing {0} (moving to {1})", item, newPath));

                    if (Directory.Exists(newPath))
                    {
                        Log(2, string.Format("Path {0} already exists", newPath));
                        dirAlreadyExists = true;
                        tempPath = newPath;
                        tempDir = new DirectoryInfo(newPath);
                    }

                    if (!dirAlreadyExists)
                    {
                        // Check if we already have a temp path laying around
                        if (GetExistingTempDir(tempPath) != null)
                        {
                            tempPath = GetExistingTempDir(tempPath);
                        }

                        if (Directory.Exists(tempPath))
                        {
                            Log(2, string.Format("Temp path {0} exists", tempPath));
                            tempDir = new DirectoryInfo(tempPath);
                        }
                        else
                        {
                            tempDir = Directory.CreateDirectory(tempPath); //  FileWatcher will not trigger on files ending in _temp
                            tempDir.Attributes = FileAttributes.Hidden;
                            Log(4, string.Format("Created {0} and changed it to hidden", tempDir.FullName));
                        }

                        Log(4, string.Format("Copying {0} to {1}", item, tempPath));
                        DirectoryCopy(item, tempPath, true);
                    }
                    long sourceSize = GetDirectorySize(item);
                    long destSize = GetDirectorySize(tempPath);

                    if (sourceSize != destSize)
                    {
                        Log(2, string.Format("Folder is incorrect size after move, this is probably because initial copy is still in progress"));
                        Log(2, string.Format("Source size      : {0}", sourceSize));
                        Log(2, string.Format("Destination size : {0}", destSize));
                    }
                    else
                    {
                        if ((tempDir.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden) // Remove hidden if present
                        {
                            tempDir.Attributes -= FileAttributes.Hidden;
                        }
                        bool copyOk = true;

                        try
                        {
                            if (!dirAlreadyExists)
                            {
                                Log(4, string.Format("Moving temp to final folder: Moving {0} to {1}", tempPath, newPath));
                                Directory.Move(tempPath, newPath);
                            }
                        }
                        catch (Exception ex)
                        {
                            tempDir.Attributes = FileAttributes.Hidden;
                            copyOk = false;
                            Log(1, ex.Message);
                        }
                        try
                        {
                            if (copyOk)
                            {
                                Log(4, string.Format("Preparing source for deletion: Moving {0} to {1}", item, item + "_delete"));
                                Directory.Move(item, item + "_delete");
                            }
                        }
                        catch (Exception ex)
                        {
                            copyOk = false;
                            Log(1, ex.Message);
                        }
                        try
                        {
                            if (copyOk)
                            {
                                Log(4, string.Format("Deleting source: Deleting {0}", item + "_delete"));
                                Directory.Delete(item + "_delete", true);
                            }
                        }
                        catch (Exception ex)
                        {
                            copyOk = false;
                            Log(1, ex.Message);
                        }

                        if (copyOk)
                        {
                            Log(3, string.Format("Successful copy"));
                            Log(4, "Triggering FileSystemUpdate()");
                            FileSystemUpdate();
                        }
                    }
                }
                else
                {
                    Log(2, string.Format("Free space on {0} after item {1} is less than {2}, skipping", dirInfo.FullName, item, minimumFreeSpace));
                }

                try
                {
                    Log(4, "Dequeing item");
                    item = moveQueue.Dequeue();
                    Log(4, string.Format("Got item {0}", item));
                }
                catch
                {
                    Log(4, "No more items in queue");
                    item = null;
                }
            }

            Log(4, string.Format("Starting moveTimer with interval {0}", moveTimerInterval));
            moveTimer.Interval = moveTimerInterval;
            moveTimer.Start();
        }

        private void timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (update)
            {
                update = false;
                Log(3, "Updating...");
                CheckFolders();
                UpdateShareFolder();
                Log(3, "Done");
            }

            timer.Interval = timerInterval;
            timer.Start();
        }

        private string GetExistingTempDir(string tempPath)
        {
            CheckForTempDirs(topFolders);
            foreach (string tempDir in foundTempDirs)
            {
                if (tempDir.Contains(tempPath))
                {
                    return tempDir;
                }
            }

            return null;
        }

        private void CheckForTempDirs(string[] topFolders)
        {
            ClearFoundTempDirs();

            topFolders.ToList().ForEach(r => {
                if (r.Contains("_temp"))
                {
                    foundTempDirs.Add(r);
                }
            });
        }

        private void ClearFoundTempDirs()
        {
            foundTempDirs = new List<string>();
        }

        private void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);
            DirectoryInfo[] dirs = dir.GetDirectories();

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            // If the destination directory doesn't exist, create it. 
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = Path.Combine(destDirName, file.Name);

                // Checks if file exists, and only overwrites if length is different
                if (File.Exists(temppath))
                {
                    FileInfo newFileInfo = new FileInfo(temppath);
                    if (file.Length != newFileInfo.Length)
                    {
                        try {
                            file.CopyTo(temppath, true);
                        }
                        catch (System.IO.IOException ex)
                        {
                            if (ex.HResult == -2147024864)
                            {
                                Log(2, string.Format("File {0} is in use, skipping", file.Name));
                            }
                        }
                    }
                }
                else
                {
                    try
                    {
                        file.CopyTo(temppath, false);
                    }
                    catch (System.IO.IOException ex)
                    {
                        if (ex.HResult == -2147024864)
                        {
                            Log(2, string.Format("File {0} is in use, skipping", file.Name));
                        }
                    }
                }
            }

            // If copying subdirectories, copy them and their contents to new location. 
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string temppath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, temppath, copySubDirs);
                }
            }
        }

        public void Start()
        {
            foreach (string tf in topFolders)
            {
                FileSystemWatcher newFsw = new FileSystemWatcher();
                newFsw.Path = tf;
                newFsw.IncludeSubdirectories = true;
                newFsw.Created += new FileSystemEventHandler(OnCreated);
                newFsw.Deleted += new FileSystemEventHandler(OnDeleted);
                newFsw.Renamed += new RenamedEventHandler(OnRenamed);
                newFsw.NotifyFilter = NotifyFilters.DirectoryName;
                newFsw.EnableRaisingEvents = true;
                Log(3, ("Added " + tf));
                listFsw.Add(newFsw);
            }
            Log(3, ("Startup complete"));
            Log(3, ("------------------------------------"));

        }

        private void FileSystemUpdate()
        {
            Log(4, "FileSystemUpdate()");
            if (!update)
            {
                update = true;
                timer.Interval = timerInterval;
                timer.Start();
                Log(4, "FileSystemUpdate(): Started timer");
            }
        }

        private void AddFolder(string name, DirectoryInfo directoryInfo)
        {
            if (dirInfo.ContainsKey(name))
            {
                dirInfo[name].Add(directoryInfo);
            }
            else
            {
                dirInfo[name] = new List<DirectoryInfo>();
                dirInfo[name].Add(directoryInfo);
            }
        }

        private long GetDirectorySize(string path)
        {
            return new DirectoryInfo(path).GetFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
        }

        private long GetFreeDiskSpace(string driveName)
        {
            return new DriveInfo(driveName).AvailableFreeSpace;
        }

        private void CheckFolders()
        {
            foreach (string line in topFolders)
            {
                string[] topResult = Directory.GetDirectories(line, "*", SearchOption.TopDirectoryOnly);
                foreach (string tr in topResult)
                {
                    DirectoryInfo topFolder = new DirectoryInfo(tr);

                    string[] result = Directory.GetDirectories(topFolder.FullName, "*", SearchOption.TopDirectoryOnly);

                    foreach (string r in result)
                    {
                        DirectoryInfo folder = new DirectoryInfo(r);
                        if ((folder.Attributes & FileAttributes.Hidden) == 0) // Not hidden
                        {
                            AddFolder(topFolder.Name, folder);
                        }
                        
                    }
                }
            }
        }

        private void UpdateShareFolder()
        {
            foreach (KeyValuePair<string, List<DirectoryInfo>> topFolder in dirInfo)
            {
                foreach (DirectoryInfo dInfo in topFolder.Value)
                {
                    string topFolderPath = shareFolder + "\\" + topFolder.Key;
                    string linkPath = shareFolder + "\\" + topFolder.Key + "\\" + dInfo.Name;
                    string destination = dInfo.FullName;

                    if (!Directory.Exists(topFolderPath))
                    {
                        Directory.CreateDirectory(topFolderPath);
                    }

                    CreateSymbolicLinkDirectory(linkPath, destination);
                    if (!Directory.Exists(linkPath))
                    {
                        Log(1, string.Format("{0} was not created. This is most likely because this program was not run with administrative rights", linkPath));
                    }
                }
            }
        }

        private void AddToMoveQueue(List<string> folders)
        {
            foreach (string folder in folders)
            {
                if (!moveQueue.Contains(folder))
                {
                    moveQueue.Enqueue(folder);
                }
            }
        }

        private List<string> GetNewFolders(string path)
        {
            List<string> result = new List<string>();

            string[] topFolders = Directory.GetDirectories(path);
            foreach (string topFolder in topFolders)
            {
                string[] folders = Directory.GetDirectories(topFolder);

                foreach (string folder in folders)
                {
                    DirectoryInfo dInfo = new DirectoryInfo(folder);
                    if ((dInfo.Attributes & FileAttributes.ReparsePoint) == 0) // Not reparse point
                    {
                        result.Add(dInfo.FullName);
                    }
                }
            }

            return result;
        }

        private DirectoryInfo FindPathWithMostFreeSpace(string[] paths)
        {
            List<DirectoryInfo> dirs = new List<DirectoryInfo>();

            foreach (string path in paths)
            {
                dirs.Add(new DirectoryInfo(path));
            }

            var sortedFreeSpace = from d in dirs orderby (new DriveInfo(d.Root.Name).AvailableFreeSpace) descending select d;

            return sortedFreeSpace.First();
        }

        public void Input(string input)
        {
            switch (input)
            {
                case "fm":
                    moveTimerInterval = 10000;
                    Log(3, "moveTimer changed to 10 sec");
                    break;
                case "debug":
                    consoleLogLevel = 4;
                    Log(3, "Debug messages enabled");
                    break;
                case "nodebug":
                    consoleLogLevel = 3;
                    Log(3, "Debug messages disabled");
                    break;
                default:
                    Log(1, "Unknown command");
                    break;
            }
        }

        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            if (!e.Name.Contains("_temp"))
            {
                FileSystemUpdate();
            }
        }

        private void OnDeleted(object sender, FileSystemEventArgs e)
        {
            if (!e.Name.Contains("_temp"))
            {
                FileSystemUpdate();
            }
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            if (!e.Name.Contains("_temp"))
            {
                FileSystemUpdate();
            }
        }


        [DllImport("kernel32.dll")]
        private static extern bool CreateSymbolicLink(string lpSymlinkFileName, string lpTargetFileName, int dwFlags);
        private void CreateSymbolicLinkDirectory(string linkPath, string destination)
        {
            if (!CreateSymbolicLink(linkPath, destination, 1) || Marshal.GetLastWin32Error() != 0)
            {
                //Console.WriteLine(Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()).Message);                
                //Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
            }
        }
    }
}
