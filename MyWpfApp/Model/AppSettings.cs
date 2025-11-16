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
        // Backing field for InputDir (can be overridden by persisted settings)
        private static string _inputDir = Path.Combine(ExeFolder, "InputDir");
        // folder path to input pdfs
        public static string InputDir
        {
            get { return _inputDir; }
            set
            {
                if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("InputDir cannot be empty", nameof(value));
                var normalized = NormalizePath(value);
                lock (_sync)
                {
                    if (!string.Equals(_inputDir, normalized, StringComparison.OrdinalIgnoreCase))
                    {
                        _inputDir = normalized;
                        try
                        {
                            // Ensure directory exists for the new input dir
                            Directory.CreateDirectory(_inputDir);
                        }
                        catch
                        {
                            // swallow - creation may fail due to permissions; let caller handle if needed
                        }
                        try
                        {
                            SaveSettings();
                        }
                        catch
                        {
                            // swallow to avoid crashing app on save failure
                        }
                    }
                }
            }
        }

        // Backing field for ArchiveDir (can be overridden by persisted settings)
        private static string _archive_dir = Path.Combine(ExeFolder, "ArchivedPDFs");
        // folder path to archive location
        public static string ArchiveDir
        {
            get { return _archive_dir; }
            set
            {
                if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("ArchiveDir cannot be empty", nameof(value));
                var normalized = NormalizePath(value);
                lock (_sync)
                {
                    if (!string.Equals(_archive_dir, normalized, StringComparison.OrdinalIgnoreCase))
                    {
                        _archive_dir = normalized;
                        try
                        {
                            Directory.CreateDirectory(_archive_dir);
                        }
                        catch
                        {
                            // swallow - creation may fail due to permissions; let caller handle if needed
                        }
                        try
                        {
                            SaveSettings();
                        }
                        catch
                        {
                            // swallow
                        }
                    }
                }
            }
        }

        // Backing field for JobDir (can be overridden by persisted settings)
        private static string _jobDir = Path.Combine(ExeFolder, "JobsDir");
        // folder path to jobs folder (configurable)
        public static string JobDir
        {
            get { return _jobDir; }
            set
            {
                if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("JobDir cannot be empty", nameof(value));
                var normalized = NormalizePath(value);
                lock (_sync)
                {
                    if (!string.Equals(_jobDir, normalized, StringComparison.OrdinalIgnoreCase))
                    {
                        _jobDir = normalized;
                        try
                        {
                            Directory.CreateDirectory(_jobDir);
                        }
                        catch
                        {
                            // swallow - creation may fail due to permissions; let caller handle if needed
                        }
                        try
                        {
                            SaveSettings();
                        }
                        catch
                        {
                            // swallow
                        }
                    }
                }
            }
        }

        // Backing field for AdobePath (can be overridden by persisted settings). Empty means unset.
        private static string _adobePath = string.Empty;
        // Path to Adobe Reader (or other PDF reader) executable or folder. Empty allowed.
        public static string AdobePath
        {
            get { return _adobePath; }
            set
            {
                var trimmed = (value ?? string.Empty).Trim();
                string normalized = string.Empty;
                if (!string.IsNullOrEmpty(trimmed))
                {
                    // Normalize non-empty paths to absolute form
                    normalized = NormalizePath(trimmed);
                }

                lock (_sync)
                {
                    if (!string.Equals(_adobePath, normalized, StringComparison.OrdinalIgnoreCase))
                    {
                        _adobePath = normalized;
                        try
                        {
                            SaveSettings();
                        }
                        catch
                        {
                            // swallow to avoid crashing app on save failure
                        }
                    }
                }
            }
        }

        //folder path to output of polling and archive system
        public static string JobWell => Path.Combine(ExeFolder, "JobWell");
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
            get { return _max_pages_for_serialization(); }
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
            // Load persisted settings first so directories are created for configured paths
            LoadSettings();
            // Ensure directories exist (uses the possibly overridden InputDir/ArchiveDir/JobDir)
            EnsureDirectoriesExist();
        }

        [DataContract]
        private class SettingsDto
        {
            [DataMember]
            public int MaxPages { get; set; }

            [DataMember]
            public string InputDir { get; set; }

            [DataMember]
            public string ArchiveDir { get; set; }

            [DataMember]
            public string JobDir { get; set; }

            [DataMember]
            public string AdobePath { get; set; }
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
                    if (dto != null)
                    {
                        if (dto.MaxPages > 0)
                        {
                            _maxPages = dto.MaxPages;
                        }
                        if (!string.IsNullOrWhiteSpace(dto.InputDir))
                        {
                            try
                            {
                                _inputDir = NormalizePath(dto.InputDir);
                            }
                            catch
                            {
                                // ignore malformed persisted path and keep default
                            }
                        }
                        if (!string.IsNullOrWhiteSpace(dto.ArchiveDir))
                        {
                            try
                            {
                                _archive_dir = NormalizePath(dto.ArchiveDir);
                            }
                            catch
                            {
                                // ignore malformed persisted path and keep default
                            }
                        }
                        if (!string.IsNullOrWhiteSpace(dto.JobDir))
                        {
                            try
                            {
                                _jobDir = NormalizePath(dto.JobDir);
                            }
                            catch
                            {
                                // ignore malformed persisted path and keep default
                            }
                        }
                        // AdobePath may be empty; allow empty string
                        if (dto.AdobePath != null)
                        {
                            try
                            {
                                _adobePath = string.IsNullOrWhiteSpace(dto.AdobePath) ? string.Empty : NormalizePath(dto.AdobePath);
                            }
                            catch
                            {
                                _adobePath = string.Empty;
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // If settings can't be read fall back to defaults
            }
        }

        private static void SaveSettings()
        {
            var dto = new SettingsDto
            {
                MaxPages = _max_pages_for_serialization(),
                InputDir = _inputDir,
                ArchiveDir = _archive_dir_for_serialization(),
                JobDir = _job_dir_for_serialization(),
                AdobePath = _adobe_path_for_serialization()
            };

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

        // Helper methods used to keep initializer expression compact and consistent
        private static int _max_pages_for_serialization() { return _maxPages; }
        private static string _archive_dir_for_serialization() { return _archive_dir; }
        private static string _job_dir_for_serialization() { return _jobDir; }
        private static string _adobe_path_for_serialization() { return _adobePath; }

        // Normalize user supplied path: if relative, treat as relative to exe folder, then return full path
        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path cannot be null or whitespace", nameof(path));
            var trimmed = path.Trim();
            string result = trimmed;
            if (!Path.IsPathRooted(trimmed))
            {
                result = Path.Combine(ExeFolder, trimmed);
            }
            // GetFullPath will normalize .. and .
            return Path.GetFullPath(result);
        }

        //call to ensure all directories exist
        public static void EnsureDirectoriesExist()
        {
            try { Directory.CreateDirectory(InputDir); } catch { }
            try { Directory.CreateDirectory(JobWell); } catch { }
            try { Directory.CreateDirectory(ArchiveDir); } catch { }
            try { Directory.CreateDirectory(JobDir); } catch { }
            try { Directory.CreateDirectory(PrinterDir); } catch { }
        }
    }
}
