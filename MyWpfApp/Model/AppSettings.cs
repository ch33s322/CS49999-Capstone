using MyWpfApp.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;

namespace MyWpfApp.Model
{
    public static class AppSettings
    {
        private static readonly string ExeFolder = AppDomain.CurrentDomain.BaseDirectory;

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
                        var old = _inputDir;
                        _inputDir = normalized;
                        try
                        {
                            // Ensure directory exists for the new input dir
                            Directory.CreateDirectory(_inputDir);
                        }
                        catch(Exception ex)
                        {
                            Debug.WriteLine($"Exception when creating input directory: {ex}");
                        }
                        try
                        {
                            SaveSettings();
                        }
                        catch(Exception ex)
                        {
                            Debug.WriteLine($"Exception when saving input directory settings: {ex}");
                        }

                        // Log the change (non-blocking and swallow errors inside logger)
                        try
                        {
                            ActivityLogger.LogChange("InputDir", old, _inputDir);
                        }
                        catch(Exception ex)
                        {
                            Debug.WriteLine($"Exception when logging directory change: {ex}");
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
                        var old = _archive_dir;
                        _archive_dir = normalized;
                        try
                        {
                            Directory.CreateDirectory(_archive_dir);
                        }
                        catch(Exception ex)
                        {
                            Debug.WriteLine($"Exception when creating archive directory: {ex}");
                        }
                        try
                        {
                            SaveSettings();
                        }
                        catch(Exception ex)
                        {
                            Debug.WriteLine($"Exception when saving archive directory: {ex}");
                        }

                        // Log the change
                        try
                        {
                            ActivityLogger.LogChange("ArchiveDir", old, _archive_dir);
                        }
                        catch(Exception ex)
                        {
                            Debug.WriteLine($"Exception when logging change of archive directory: {ex}");
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
                        var old = _jobDir;
                        _jobDir = normalized;
                        try
                        {
                            Directory.CreateDirectory(_jobDir);
                        }
                        catch(Exception ex)
                        {
                            Debug.WriteLine($"Exception when creating directory: {ex}");
                        }
                        try
                        {
                            SaveSettings();
                        }
                        catch(Exception ex)
                        {
                            Debug.WriteLine($"Exception when saving directory settings: {ex}");
                        }

                        // Log the change
                        try
                        {
                            ActivityLogger.LogChange("JobDir", old, _jobDir);
                        }
                        catch(Exception ex)
                        {
                            Debug.WriteLine($"Exception when logging directory change: {ex}");
                        }
                    }
                }
            }
        }

        // Backing field for AdobePath (overridden by persisted settings)
        private static string _adobePath = string.Empty;
        // Path to Adobe Reader executable or folder
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

                    // Try to resolve to an actual executable path if user provided a folder or partial path
                    try
                    {
                        var resolvedExe = ResolveAdobeExecutablePath(normalized);
                        if (!string.IsNullOrEmpty(resolvedExe))
                        {
                            normalized = resolvedExe;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Exception when resolving AdobePath: {ex}");
                    }
                }

                lock (_sync)
                {
                    if (!string.Equals(_adobePath, normalized, StringComparison.OrdinalIgnoreCase))
                    {
                        var old = _adobePath;
                        _adobePath = normalized;
                        try
                        {
                            SaveSettings();
                        }
                        catch(Exception ex)
                        {
                            Debug.WriteLine($"Exception when saving AdobePath settings: {ex}");
                        }

                        // Log the change
                        try
                        {
                            ActivityLogger.LogChange("AdobePath", old, _adobePath);
                        }
                        catch(Exception ex)
                        {
                            Debug.WriteLine($"Exception when logging AdobePath change: {ex}");
                        }
                    }
                }
            }
        }

        // Attempt to resolve given path into an Adobe executable full path
        // Accepts: direct EXE path, directory containing a known EXE, path combined with known Adobe EXE names
        private static string ResolveAdobeExecutablePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try
            {
                // If it's already an existing file, accept it
                if (File.Exists(path)) return Path.GetFullPath(path);

                // If it's a directory, search for common Adobe EXE names
                if (Directory.Exists(path))
                {
                    var candidates = new[] { "AcroRd32.exe", "AcroRd64.exe", "Acrobat.exe" };
                    foreach (var c in candidates)
                    {
                        var p = Path.Combine(path, c);
                        if (File.Exists(p)) return Path.GetFullPath(p);
                    }
                }

                // Same as above, but if user doesn't enter trailing backslash
                var fallbackCandidates = new[] { "AcroRd32.exe", "AcroRd64.exe", "Acrobat.exe" };
                foreach (var c in fallbackCandidates)
                {
                    try
                    {
                        var combined = Path.Combine(path, c);
                        if (File.Exists(combined)) return Path.GetFullPath(combined);
                    }
                    catch(Exception ex)
                    {
                        Debug.WriteLine($"Exception when getting full Adobe EXE path: {ex}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception when resolving Adobe path: {ex}");
            }
            return null;
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
                        var old = _maxPages;
                        _maxPages = value;
                        try
                        {
                            SaveSettings();
                        }
                        catch(Exception ex)
                        {
                            Debug.WriteLine($"Exception when saving new maxPages value: {ex}");
                        }

                        // Log the change
                        try
                        {
                            ActivityLogger.LogChange("MaxPages", old, value);
                        }
                        catch(Exception ex)
                        {
                            Debug.WriteLine($"Exception when logging maxPages change: {ex}");
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
                            catch(Exception ex)
                            {
                                Debug.WriteLine($"Exception when loading settings (InputDir): {ex}");
                            }
                        }
                        if (!string.IsNullOrWhiteSpace(dto.ArchiveDir))
                        {
                            try
                            {
                                _archive_dir = NormalizePath(dto.ArchiveDir);
                            }
                            catch(Exception ex)
                            {
                                Debug.WriteLine($"Exception when loading settings (ArchiveDir): {ex}");
                            }
                        }
                        if (!string.IsNullOrWhiteSpace(dto.JobDir))
                        {
                            try
                            {
                                _jobDir = NormalizePath(dto.JobDir);
                            }
                            catch(Exception ex)
                            {
                                Debug.WriteLine($"Exception when loading settings (JobDir): {ex}");
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
                // Fallback, try to copy/overwrite
                try
                {
                    File.Copy(temp, SettingsFile, overwrite: true);
                    File.Delete(temp);
                }
                catch(Exception ex)
                {
                    Debug.WriteLine($"Exception when attempting to persist settings: {ex}");
                }
            }
        }

        // Helper methods used to keep initializer expression compact and consistent
        private static int _max_pages_for_serialization() { return _maxPages; }
        private static string _archive_dir_for_serialization() { return _archive_dir; }
        private static string _job_dir_for_serialization() { return _jobDir; }
        private static string _adobe_path_for_serialization() { return _adobePath; }

        // Normalize user supplied path
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
            try { Directory.CreateDirectory(InputDir); } catch(Exception ex) { Debug.WriteLine($"Exception when checking directory: {ex}"); }
            try { Directory.CreateDirectory(JobWell); } catch (Exception ex) { Debug.WriteLine($"Exception when checking directory: {ex}"); }
            try { Directory.CreateDirectory(ArchiveDir); } catch (Exception ex) { Debug.WriteLine($"Exception when checking directory: {ex}"); }
            try { Directory.CreateDirectory(JobDir); } catch (Exception ex) { Debug.WriteLine($"Exception when checking directory: {ex}"); }
            try { Directory.CreateDirectory(PrinterDir); } catch (Exception ex) { Debug.WriteLine($"Exception when checking directory: {ex}"); }
        }
    }
}
