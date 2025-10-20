using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyWpfApp.Model
{
    public static class AppSettings
    {
        private static readonly string ExeFolder = AppDomain.CurrentDomain.BaseDirectory;

        /*Default folder paths*/
        //folder path to input pdfs
        public static string InputDir => Path.Combine(ExeFolder, "InputDir");
        //folder path to output of polling and archive system
        public static string JobWell => Path.Combine(ExeFolder, "JobWell");
        //folder path to archive location
        public static string ArchiveDir => Path.Combine(ExeFolder, "ArchivedPDFs");
        //folder path to jobs folder
        public static string JobDir => Path.Combine(ExeFolder, "JobsDir");
        //number of pages we are doing per split
        public static int MaxPages { get; set; } = 1000;

        //directory: 
           /**/
        /*TODO add ability to customize*/


        //call to ensure all directories exist
        public static void EnsureDirectoriesExist()
        {
            Directory.CreateDirectory(InputDir);
            Directory.CreateDirectory(JobWell);
            Directory.CreateDirectory(ArchiveDir);
            Directory.CreateDirectory(JobDir);
        }
    }
}
