using MyWpfApp.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyWpfApp.Model
{
    public class AppSettings
    {
        //singleton instance
        private static AppSettings _instance;
        private static readonly object _lock = new object();
        public static AppSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new AppSettings();
                        }
                    }
                }
                return _instance;
            }
        }
        //default constructor
        private AppSettings()
        {
            InputDir = Path.Combine(ExeFolder, "InputDir");
            JobWell = Path.Combine(ExeFolder, "JobWell");
            ArchiveDir = Path.Combine(ExeFolder, "ArchivedPDFs");
            JobDir = Path.Combine(ExeFolder, "JobsDir");
            PrinterDir = Path.Combine(ExeFolder, "PrinterDir");
            PrinterStoreFile = Path.Combine(ExeFolder, "printers.json");
            MaxPages = 1000;
        }

        //data members
        private readonly string ExeFolder = AppDomain.CurrentDomain.BaseDirectory;
        public string InputDir { get; set; }
        public string JobWell {  get; set; }
        public string ArchiveDir { get; set; }
        public string JobDir { get; set; }
        public string PrinterDir { get; set; }
        public string PrinterStoreFile { get; set; }
        public int MaxPages { get; set; }
        /*Default folder paths*/


        //call to ensure all directories exist
        public void EnsureDirectoriesExist()
        {
            Directory.CreateDirectory(InputDir);
            Directory.CreateDirectory(JobWell);
            Directory.CreateDirectory(ArchiveDir);
            Directory.CreateDirectory(JobDir);
            Directory.CreateDirectory(PrinterDir);
        }
    }
}

