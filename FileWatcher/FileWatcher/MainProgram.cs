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
        private Dictionary<string, List<DirectoryInfo>> dirInfo;
        private string[] topFolders;
        private string shareFolder;

        public MainProgram()
        {
            listFsw = new List<FileSystemWatcher>();
            dirInfo = new Dictionary<string, List<DirectoryInfo>>();
            timer = new Timer();
            timer.Interval = 1000;
            timer.Elapsed += timer_Elapsed;
            timer.AutoReset = false;
            timer.Start();
            topFolders = File.ReadAllLines("ListOfDirectories.txt");
            shareFolder = File.ReadAllLines("ShareFolder.txt")[0];
        }

        private void timer_Elapsed(object sender, ElapsedEventArgs e)
        {

            if (update)
            {
                update = false;
                Console.Clear();
                Console.WriteLine("Updating...");
                CheckFolders();
                UpdateShareFolder();
                Console.WriteLine("Done.");
            }

            timer.Start();
        }

        public void Start()
        {
            CheckForNewFilesAndFolders(shareFolder);

            if (false)
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

        private void CheckForNewFilesAndFolders(string path)
        {
            string[] topFolders = Directory.GetDirectories(path);
            foreach (string topFolder in topFolders)
            {
                string[] folders = Directory.GetDirectories(topFolder);

                foreach (string folder in folders)
                {
                    DirectoryInfo dInfo = new DirectoryInfo(folder);
                    if ((dInfo.Attributes & FileAttributes.ReparsePoint) == 0) // Not reparse point
                    {
                        Console.WriteLine("{0} > Not a symbolic link", dInfo.FullName);
                    }
                }
            }
        }

        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            FileSystemUpdate();
        }

        private void OnDeleted(object sender, FileSystemEventArgs e)
        {
            FileSystemUpdate();
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            FileSystemUpdate();
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
