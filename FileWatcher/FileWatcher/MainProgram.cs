﻿using System;
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
        private int moveTimerInterval = 60000;

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

        void moveTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            int minimumFreeSpace = 1024;
            
            AddToMoveQueue(GetNewFolders(shareFolder));

            string item = null;
            try
            {
                item = moveQueue.Dequeue();
            } catch { }

            while (item != null)
            {
                long itemSize = GetDirectorySize(item);
                DirectoryInfo dirInfo = FindPathWithMostFreeSpace(topFolders);

                long sizeAfterItemMb = (new DriveInfo(dirInfo.Root.Name).AvailableFreeSpace - itemSize) / 1024 / 1024;

                if (sizeAfterItemMb > minimumFreeSpace)
                {
                    string newPath = item.Replace(shareFolder, dirInfo.FullName);
                    bool dirAlreadyExists = false;
                    string tempPath = newPath + "_temp";
                    DirectoryInfo tempDir = null;

                    Console.WriteLine("Processing {0} (moving to {1})", item, newPath);

                    if (Directory.Exists(newPath))
                    {
                        Console.WriteLine("Path {0} already exists", newPath);
                        dirAlreadyExists = true;
                    }

                    if (!dirAlreadyExists)
                    {
                        if (Directory.Exists(tempPath))
                        {
                            Console.WriteLine("Temp path {0} exists", tempPath);
                            tempDir = new DirectoryInfo(tempPath);
                        }
                        else
                        {
                            tempDir = Directory.CreateDirectory(tempPath); //  FileWatcher will not trigger on files ending in _temp
                            tempDir.Attributes = FileAttributes.Hidden;
                        }

                        DirectoryCopy(item, tempPath, true);
                    }
                    long sourceSize = GetDirectorySize(item);
                    long destSize = GetDirectorySize(tempPath);

                    if (sourceSize != destSize)
                    {
                        Console.WriteLine("Folder is incorrect size after move, this is probably because initial copy is still in progress");
                        Console.WriteLine("Source size      : {0}", sourceSize);
                        Console.WriteLine("Destination size : {0}", destSize);
                    }
                    else
                    {
                        tempDir.Attributes -= FileAttributes.Hidden;
                        bool copyOk = true;

                        try
                        {
                            if (!dirAlreadyExists)
                            {
                                Directory.Move(tempPath, newPath);
                            }
                        }
                        catch (IOException ex)
                        {
                            tempDir.Attributes = FileAttributes.Hidden;
                            copyOk = false;
                            Console.WriteLine(ex.Message);
                        }
                        try
                        {
                            if (copyOk)
                            {
                                Directory.Move(item, item + "_delete");
                            }
                        }
                        catch (IOException ex)
                        {
                            copyOk = false;
                            Console.WriteLine(ex.Message);
                        }
                        try
                        {
                            if (copyOk)
                            {
                                Directory.Delete(item + "_delete", true);
                            }
                        }
                        catch (IOException ex)
                        {
                            copyOk = false;
                            Console.WriteLine(ex.Message);
                        }

                        if (copyOk)
                        {
                            Console.WriteLine("Successful copy");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Free space on {0} after item {1} is less than {2}, skipping", dirInfo.FullName, item, minimumFreeSpace);
                }

                try
                {
                    item = moveQueue.Dequeue();
                }
                catch
                {
                    item = null;
                }
            }

            moveTimer.Interval = moveTimerInterval;
            moveTimer.Start();
        }

        private void timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (update)
            {
                update = false;
                Console.WriteLine("{0}> Updating...", DateTime.Now.ToString("HH:mm:ss"));
                CheckFolders();
                UpdateShareFolder();
                Console.WriteLine("{0}> Done", DateTime.Now.ToString("HH:mm:ss"));
            }

            timer.Start();
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
                                Console.WriteLine("File {0} is in use, skipping", file.Name);
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
                            Console.WriteLine("File {0} is in use, skipping", file.Name);
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
                Console.WriteLine("Added " + tf);
                listFsw.Add(newFsw);
            }
            Console.WriteLine("Startup complete");
            Console.WriteLine("------------------------------------");

        }

        private void FileSystemUpdate()
        {
            update = true;
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
                    //Console.WriteLine(string.Format("{0} => {1}", linkPath, destination));

                    if (!Directory.Exists(topFolderPath))
                    {
                        Directory.CreateDirectory(topFolderPath);
                    }

                    CreateSymbolicLinkDirectory(linkPath, destination);
                    if (!Directory.Exists(linkPath))
                    {
                        Console.WriteLine("{0} was not created. This is most likely because this program was not run with administrative rights", linkPath);
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
                    Console.WriteLine("moveTimer changed to 10 sec");
                    break;
                default:
                    Console.WriteLine("Unknown command");
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
