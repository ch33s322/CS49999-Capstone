using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

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
        //folder path to printers folder
        public static string PrinterDir => Path.Combine(ExeFolder, "PrinterDir");
        // file that persists the list of printers and their associated jobs
        public static string PrinterStoreFile => Path.Combine(PrinterDir, "printers.json");

        // where app-level settings are persisted
        private static readonly string SettingsFile = Path.Combine(ExeFolder, "appsettings.json");

        // Backing field and sync for MaxPages persistence
        private static readonly object _sync = new object();
        private static int _maxPages = 1000;

        // number of pages per split
        public static int MaxPages
        {
            get { return _maxPages; }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(value), "MaxPages must be greater than 0");
                lock (_sync)
                {
                    if (_maxPages != value)
                    {
                        _maxPages = value;
                        try
                        {
                            SaveSettings();
                        }
                        catch
                        {
                        }
                    }
                }
            }
        }

        // Static ctor loads persisted settings
        static AppSettings()
        {
            // Ensure directories exist
            EnsureDirectoriesExist();
            LoadSettings();
        }

        [DataContract]
        private class SettingsDto
        {
            [DataMember]
            public int MaxPages { get; set; }
        }

        private static void LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsFile))
                {
                    // Nothing to load so keep defaults
                    return;
                }

                using (var fs = File.OpenRead(SettingsFile))
                {
                    var ser = new DataContractJsonSerializer(typeof(SettingsDto));
                    var dto = ser.ReadObject(fs) as SettingsDto;
                    if (dto != null && dto.MaxPages > 0)
                    {
                        _maxPages = dto.MaxPages;
                    }
                }
            }
            catch (Exception)
            {
                // If settings cant be read fall back to defaults
            }
        }

        private static void SaveSettings()
        {
            var dto = new SettingsDto { MaxPages = _maxPages };

            // Serialize to a temp file then save to final
            var temp = SettingsFile + ".tmp";
            using (var ms = new MemoryStream())
            {
                var ser = new DataContractJsonSerializer(typeof(SettingsDto));
                ser.WriteObject(ms, dto);
                var bytes = ms.ToArray();
                // Ensure directory exists
                var dir = Path.GetDirectoryName(SettingsFile);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllBytes(temp, bytes);
            }

            try
            {
                // Replace existing file atomically when possible
                if (File.Exists(SettingsFile))
                {
                    File.Replace(temp, SettingsFile, null);
                }
                else
                {
                    File.Move(temp, SettingsFile);
                }
            }
            catch
            {
                // Fallback: try to copy/overwrite
                try
                {
                    File.Copy(temp, SettingsFile, overwrite: true);
                    File.Delete(temp);
                }
                catch
                {
                    // If persistence fails, swallow to avoid crashing the app.
                }
            }
        }

        //call to ensure all directories exist
        public static void EnsureDirectoriesExist()
        {
            Directory.CreateDirectory(InputDir);
            Directory.CreateDirectory(JobWell);
            Directory.CreateDirectory(ArchiveDir);
            Directory.CreateDirectory(JobDir);
            Directory.CreateDirectory(PrinterDir);
        }
    }
}
