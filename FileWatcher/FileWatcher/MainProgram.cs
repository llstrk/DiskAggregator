using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Timers;

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
            foreach (string tf in topFolders) {
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
                        AddFolder(topFolder.Name, folder);
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
                    Console.WriteLine(string.Format("{0}\\{1} => {2}", topFolder.Key, dInfo.Name, dInfo.FullName));
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
    }
}
