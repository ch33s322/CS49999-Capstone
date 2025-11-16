using MyWpfApp.Model;
using MyWpfApp.Utilities;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using Printer.ViewModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Printing;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using PrinterClass = Printer.Model.Printer;

namespace MyWpfApp.Model
{
    public class PrintManager
    {
        private readonly object _lock = new object();
        private List<PrinterClass> _printers = new List<PrinterClass>();

        private const string UnassignedPrinterName = "";
        public string AdobeReaderPath { get; set; } = @"C:\\Program Files\\Adobe\\Acrobat DC\\Acrobat\\Acrobat.exe";

        // Raised after any change to jobs (add/remove/move or split file deletion).
        public event EventHandler JobsChanged;

        public PrintManager()
        {
            AppSettings.EnsureDirectoriesExist();
            LoadPrinterStore();
            EnsureUnassignedPrinterExists();

            var detected = GetAdobeReaderPath();
            if (!string.IsNullOrWhiteSpace(detected))
            {
                AdobeReaderPath = detected;
            }
        }

        // Ensure that the unassigned printer entry always exists
        private void EnsureUnassignedPrinterExists()
        {
            lock (_lock)
            {
                if (!_printers.Any(p => string.Equals(p.Name ?? string.Empty, UnassignedPrinterName, StringComparison.OrdinalIgnoreCase)))
                {
                    var unassigned = new PrinterClass { Name = UnassignedPrinterName, Status = "Unassigned" };
                    _printers.Add(unassigned);
                    SavePrinterStore();
                    RaiseJobsChanged();
                }
            }
        }

        // Raise the JobsChanged event, marshaling to the UI thread if necessary
        private void RaiseJobsChanged()
        {
            // Marshal to UI thread
            var handler = JobsChanged;
            if (handler == null) return;

            try
            {
                var app = Application.Current;
                if (app != null && app.Dispatcher != null && !app.Dispatcher.CheckAccess())
                {
                    app.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try { handler(this, EventArgs.Empty); } catch { }
                    }));
                }
                else
                {
                    handler(this, EventArgs.Empty);
                }
            }
            catch(Exception ex)
            {
                Debug.WriteLine($"Exception when invoking handler for change in jobs: {ex}");
            }
        }

        // Queue a new job for printing, check for duplicates
        public void QueueJob(Job job)
        {
            if (job == null) throw new ArgumentNullException(nameof(job));

            lock (_lock)
            {
                var targetName = string.IsNullOrWhiteSpace(job.printerName) ? UnassignedPrinterName : job.printerName;
                var target = _printers.FirstOrDefault(p => string.Equals(p.Name, targetName, StringComparison.OrdinalIgnoreCase));

                if (target == null)
                {
                    target = new PrinterClass { Name = targetName, Status = "Unknown" };
                    _printers.Add(target);
                }

                var existingJob = _printers.SelectMany(p => p.Jobs).FirstOrDefault(j => j.orgPdfName == job.orgPdfName);

                if (existingJob == null)
                {
                    job.printerName = target.Name;
                    target.Jobs.Add(job);
                    SavePrinterStore();
                    RaiseJobsChanged();
                }
                else
                {
                    MessageBox.Show("A job for this PDF already exists. Duplicate jobs are not allowed.", "Queue Job", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        // Remove (delete) a job by its jobId
        public void ReleaseJob(Guid jobId)
        {
            lock (_lock)
            {
                bool changed = false;
                foreach (var p in _printers)
                {
                    var existed = p.Jobs.RemoveAll(j => j.jobId == jobId) > 0;
                    if (existed) changed = true;
                }

                if (changed)
                {
                    SavePrinterStore();
                    RaiseJobsChanged();
                }
            }
        }

        public void SetJobSimplexFlag(Guid jobId, bool flag)
        {
            lock (_lock)
            {
                var job = _printers.SelectMany(p => p.Jobs).FirstOrDefault(j => j.jobId == jobId);
                if (job == null) throw new KeyNotFoundException("Job not found");
                job.Simplex = flag;
                SavePrinterStore();
                RaiseJobsChanged();
            }
        }

        // Remove a single split PDF, deleting parent job if it becomes empty
        public bool RemoveSplitFileFromJob(Guid jobId, string splitPdfName)
        {
            if (string.IsNullOrWhiteSpace(splitPdfName)) throw new ArgumentException(nameof(splitPdfName));

            bool changed = false;

            lock (_lock)
            {
                PrinterClass owningPrinter = null;
                Job job = null;
                foreach (var p in _printers)
                {
                    job = p.Jobs.FirstOrDefault(j => j.jobId == jobId);
                    if (job != null)
                    {
                        owningPrinter = p;
                        break;
                    }
                }
                if (job == null) throw new KeyNotFoundException("Job not found");

                var matched = job.fileNames.FirstOrDefault(f => string.Equals(f, splitPdfName, StringComparison.OrdinalIgnoreCase));
                if (matched == null)
                {
                    return false;
                }

                var jobDirFull = Path.GetFullPath(AppSettings.JobDir);
                var fullPath = Path.GetFullPath(Path.Combine(AppSettings.JobDir, matched));
                if (!fullPath.StartsWith(jobDirFull, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Cannot delete files outside the JobDir directory.");

                bool deletedOnDisk = false;
                if (File.Exists(fullPath))
                {
                    try
                    {
                        File.Delete(fullPath);
                        deletedOnDisk = true;
                    }
                    catch (Exception ex)
                    {
                        try { ActivityLogger.LogAction("RemoveSplitFileError", $"Failed to delete '{fullPath}': {ex.Message}"); } catch { }
                        return false;
                    }
                }

                job.fileNames.RemoveAll(f => string.Equals(f, matched, StringComparison.OrdinalIgnoreCase));
                changed = true;

                bool jobDeleted = false;
                if (job.fileNames.Count == 0 && owningPrinter != null)
                {
                    owningPrinter.Jobs.RemoveAll(j => j.jobId == job.jobId);
                    jobDeleted = true;
                }

                SavePrinterStore();

                try
                {
                    ActivityLogger.LogAction("RemoveSplitFile",
                        $"Job {jobId}: Removed file '{matched}' (deletedOnDisk:{deletedOnDisk}) (jobDeleted:{jobDeleted})");
                }
                catch { }
            }

            if (changed) RaiseJobsChanged();
            return true;
        }

        // Set the printer for a job, used when moving job to new printer
        public void SetJobPrinter(Guid jobId, string printerName)
        {
            if (printerName == null) throw new ArgumentNullException(nameof(printerName));

            lock (_lock)
            {
                Job job = null;
                foreach (var p in _printers)
                {
                    var existing = p.Jobs.FirstOrDefault(j => j.jobId == jobId);
                    if (existing != null)
                    {
                        job = existing;
                        p.Jobs.Remove(existing);
                        break;
                    }
                }

                if (job == null) throw new KeyNotFoundException("Job not found; queue the job before assigning");

                var targetName = string.IsNullOrWhiteSpace(printerName) ? UnassignedPrinterName : printerName;
                var target = _printers.FirstOrDefault(p => string.Equals(p.Name, targetName, StringComparison.OrdinalIgnoreCase));
                if (target == null)
                {
                    target = new PrinterClass { Name = targetName, Status = "Unknown" };
                    _printers.Add(target);
                }

                job.printerName = target.Name;
                target.Jobs.Add(job);

                SavePrinterStore();
            }

            RaiseJobsChanged();
        }

        // Add a new printer to the application
        public bool AddPrinter(string PrinterName)
        {
            if (string.IsNullOrWhiteSpace(PrinterName)) throw new ArgumentException(nameof(PrinterName));

            if (!Directory.Exists(AppSettings.PrinterDir))
            {
                Directory.CreateDirectory(AppSettings.PrinterDir);
            }

            string printerPath = Path.Combine(AppSettings.PrinterDir, PrinterName);
            if (!Directory.Exists(printerPath))
            {
                Directory.CreateDirectory(printerPath);
            }

            lock (_lock)
            {
                if (!_printers.Any(p => string.Equals(p.Name, PrinterName, StringComparison.OrdinalIgnoreCase)))
                {
                    var newPrinter = new PrinterClass { Name = PrinterName, Status = "Unknown" };
                    _printers.Add(newPrinter);
                    SavePrinterStore();
                    RaiseJobsChanged();

                    try { ActivityLogger.LogAction("AddPrinter", $"Printer '{PrinterName}' added"); } catch { }
                    return true;
                }
            }

            return false;
        }

        // Remove an existing printer, moving its jobs to unassigned
        public PrinterClass RemovePrinter(string printerName)
        {
            if (string.IsNullOrWhiteSpace(printerName)) throw new ArgumentException(nameof(printerName));
            PrinterClass removed = null;

            lock (_lock)
            {
                var entry = _printers.FirstOrDefault(p => string.Equals(p.Name, printerName, StringComparison.OrdinalIgnoreCase));
                if (entry == null) return null;

                var unassigned = _printers.FirstOrDefault(p => string.Equals(p.Name, UnassignedPrinterName, StringComparison.OrdinalIgnoreCase));
                if (unassigned == null)
                {
                    unassigned = new PrinterClass { Name = UnassignedPrinterName, Status = "Unassigned" };
                    _printers.Add(unassigned);
                }

                int movedCount = 0;
                foreach (var j in entry.Jobs)
                {
                    j.printerName = unassigned.Name;
                    if (!unassigned.Jobs.Any(x => x.jobId == j.jobId))
                    {
                        unassigned.Jobs.Add(j);
                        movedCount++;
                    }
                }

                _printers.Remove(entry);
                removed = entry;
                SavePrinterStore();

                try
                {
                    string printerPath = Path.Combine(AppSettings.PrinterDir, printerName);
                    if (Directory.Exists(printerPath)) Directory.Delete(printerPath, true);
                }
                catch { }

                try { ActivityLogger.LogAction("RemovePrinter", $"Printer '{printerName}' removed; {movedCount} job(s) moved to unassigned"); } catch { }
            }

            if (removed != null) RaiseJobsChanged();
            return removed;
        }

        public List<Job> GetJobs()
        {
            lock (_lock)
            {
                return _printers.SelectMany(p => p.Jobs).ToList();
            }
        }

        public List<Job> GetJobsForPrinter(string printer)
        {
            lock (_lock)
            {
                var name = string.IsNullOrWhiteSpace(printer) ? UnassignedPrinterName : printer;
                var p = _printers.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
                if (p == null) return new List<Job>();
                return p.Jobs.ToList();
            }
        }

        public List<string> getAllPrinters()
        {
            lock (_lock)
            {
                return _printers.Select(p => p.Name).ToList();
            }
        }

        [DataContract]
        private class PersistedPrinterDto
        {
            [DataMember] public string Name { get; set; }
            [DataMember] public string Status { get; set; }
            [DataMember] public List<Job> Jobs { get; set; } = new List<Job>();
        }

        private void LoadPrinterStore()
        {
            lock (_lock)
            {
                try
                {
                    if (!Directory.Exists(AppSettings.PrinterDir)) Directory.CreateDirectory(AppSettings.PrinterDir);

                    var path = AppSettings.PrinterStoreFile;
                    if (!File.Exists(path))
                    {
                        _printers = new List<PrinterClass>();
                        return;
                    }

                    using (var fs = File.OpenRead(path))
                    {
                        var ser = new DataContractJsonSerializer(typeof(List<PersistedPrinterDto>));
                        var dtoList = ser.ReadObject(fs) as List<PersistedPrinterDto> ?? new List<PersistedPrinterDto>();

                        _printers = dtoList.Select(d =>
                        {
                            var p = new PrinterClass
                            {
                                Name = d.Name,
                                Status = d.Status ?? "Unknown",
                                Jobs = d.Jobs ?? new List<Job>()
                            };
                            return p;
                        }).ToList();
                    }
                }
                catch
                {
                    _printers = new List<PrinterClass>();
                }
            }
        }

        private void SavePrinterStore()
        {
            lock (_lock)
            {
                try
                {
                    var path = AppSettings.PrinterStoreFile;
                    var dtoList = _printers.Select(p => new PersistedPrinterDto
                    {
                        Name = p.Name,
                        Status = p.Status,
                        Jobs = p.Jobs ?? new List<Job>()
                    }).ToList();

                    using (var fs = File.Create(path))
                    {
                        var ser = new DataContractJsonSerializer(typeof(List<PersistedPrinterDto>));
                        ser.WriteObject(fs, dtoList);
                    }
                }
                catch(Exception ex)
                {
                    Debug.WriteLine($"Exception when saving printer store: {ex}");
                }
            }
        }

        // Attempt to locate Adobe Reader installation via registry
        private string GetAdobeReaderPath()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\AcroRd32.exe"))
                {
                    if (key != null)
                    {
                        var path = key.GetValue("") as string;
                        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                            return path;
                    }
                }

                using (var key = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64)
                    .OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\AcroRd32.exe"))
                {
                    if (key != null)
                    {
                        var path = key.GetValue("") as string;
                        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                            return path;
                    }
                }

                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths\AcroRd32.exe"))
                {
                    if (key != null)
                    {
                        var path = key.GetValue("") as string;
                        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                            return path;
                    }
                }

                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\Acrobat.exe"))
                {
                    if (key != null)
                    {
                        var path = key.GetValue("") as string;
                        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                            return path;
                    }
                }
            }
            catch(Exception ex)
            {
                Debug.WriteLine($"Exception when attempting to locate Adobe Reader path via registry: {ex}");
            }

            return null;
        }

        public bool PrintJob(string pdfName, string printerName)
        {
            if (string.IsNullOrWhiteSpace(pdfName)) throw new ArgumentException(nameof(pdfName));

            var inputPdfPath = Path.Combine(AppSettings.JobDir, pdfName);
            if (!File.Exists(inputPdfPath)) throw new FileNotFoundException("PDF file not found in JobWell.", inputPdfPath);
            if (string.IsNullOrWhiteSpace(AdobeReaderPath)) throw new InvalidOperationException("Adobe Reader path not set.");
            if (!File.Exists(AdobeReaderPath)) throw new FileNotFoundException("Adobe Reader executable not found.", AdobeReaderPath);

            string arguments = $"/t \"{inputPdfPath}\" \"{printerName}\"";

            var psi = new ProcessStartInfo
            {
                FileName = AdobeReaderPath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = Path.GetDirectoryName(AdobeReaderPath) ?? Environment.CurrentDirectory
            };

            int pageCount = 0;
            try
            {
                using (PdfDocument input = PdfReader.Open(inputPdfPath, PdfDocumentOpenMode.Import))
                {
                    pageCount = input.PageCount;
                }
            }
            catch (Exception ex)
            {
                var msg = $"Failed to open PDF '{inputPdfPath}' before printing: {ex.Message}";
                throw new InvalidOperationException(msg, ex);
            }

            Debug.WriteLine("starting process for print");
            Process proc = null;
            try
            {
                try
                {
                    proc = Process.Start(psi);
                    if (proc == null)
                        throw new InvalidOperationException("Failed to start Adobe Reader process (Process.Start returned null).");
                }
                catch (Exception startEx)
                {
                    var diagnostics = new StringBuilder();
                    diagnostics.AppendLine("Failed to start Adobe Reader process.");
                    diagnostics.AppendLine($"FileName: {psi.FileName}");
                    diagnostics.AppendLine($"Arguments: {psi.Arguments}");
                    diagnostics.AppendLine($"UseShellExecute: {psi.UseShellExecute}");
                    diagnostics.AppendLine($"CreateNoWindow: {psi.CreateNoWindow}");
                    diagnostics.AppendLine($"WorkingDirectory: {psi.WorkingDirectory}");
                    diagnostics.AppendLine($"AdobeReaderPath exists: {File.Exists(AdobeReaderPath)}");
                    try { diagnostics.AppendLine($"AdobeReaderPath attrs: {File.GetAttributes(AdobeReaderPath)}"); } catch { }
                    diagnostics.AppendLine($"CurrentUser: {Environment.UserName}");
                    diagnostics.AppendLine($"OSVersion: {Environment.OSVersion}");
                    diagnostics.AppendLine($"Exception: {startEx.GetType().FullName}: {startEx.Message}");
                    diagnostics.AppendLine(startEx.ToString());

                    var diagMsg = diagnostics.ToString();
                    try { ActivityLogger.LogAction("PrintProcessStartError", diagMsg); } catch { }
                    Debug.WriteLine(diagMsg);

                    throw new InvalidOperationException("Failed to start Adobe Reader process. See InnerException and Debug output for details.", startEx);
                }

                // Hard timeout of 30 minutes for print job to complete
                TimeSpan timeoutMs = TimeSpan.FromMinutes(30);

                bool completed;
                try
                {
                    completed = WaitForPrintJobCompletion(printerName, pdfName, pageCount, timeoutMs);
                }
                catch
                {
                    try { if (proc != null && !proc.HasExited) proc.Kill(); Debug.WriteLine("process timed out"); } catch { }
                    throw;
                }

                if (!completed)
                {
                    try { if (proc != null && !proc.HasExited) proc.Kill(); } catch(Exception ex) { Debug.WriteLine($"Exception when printing: {ex}"); }
                    return false;
                }

                try
                {
                    if (proc.HasExited && proc.ExitCode != 0)
                        throw new InvalidOperationException($"Adobe Reader exited with code {proc.ExitCode} when printing.");
                }
                catch (InvalidOperationException) { throw; }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Failed to verify Adobe Reader process state.", ex);
                }

                // Successful print: delete the split PDF and possibly the empty job.
                try
                {
                    Guid foundJobId;
                    if (TryFindJobIdBySplitFile(pdfName, out foundJobId))
                    {
                        var removed = RemoveSplitFileFromJob(foundJobId, pdfName);
                        try
                        {
                            ActivityLogger.LogAction("AutoDeleteSplitAfterPrint",
                                $"Printed and removed '{pdfName}' from job {foundJobId} (removed:{removed})");
                        }
                        catch(Exception ex)
                        {
                            Debug.WriteLine($"Exception when looking for job by split PDF file: {ex}");
                        }
                    }
                    else
                    {
                        TryDeleteSplitFileFromDisk(pdfName);
                        try
                        {
                            ActivityLogger.LogAction("AutoDeleteSplitAfterPrintOrphan",
                                $"Printed and deleted untracked split '{pdfName}'");
                        }
                        catch(Exception ex)
                        {
                            Debug.WriteLine($"Exception when trying to automatically delete split PDF after printing: {ex}");
                        }
                    }
                }
                catch(Exception ex)
                {
                    Debug.WriteLine($"Exception when attempting to delete after printing: {ex}");
                }

                return true;
            }
            finally
            {
                try { if (proc != null && !proc.HasExited) proc.Kill(); } catch(Exception ex) { Debug.WriteLine($"Exception when printing: {ex}"); }
            }
        }

        private bool WaitForPrintJobCompletion(string printerName, string documentName, int pagesToPrint, TimeSpan timeout)
        {
            var server = new LocalPrintServer();
            PrintQueue queue;
            try
            {
                queue = server.GetPrintQueue(printerName);
            }
            catch
            {
                return false;
            }

            var sw = Stopwatch.StartNew();
            int? observedJobId = null;

            while (sw.Elapsed < timeout)
            {
                try
                {
                    queue.Refresh();
                    var jobs = queue.GetPrintJobInfoCollection().Cast<PrintSystemJobInfo>().ToList();

                    var job = jobs.FirstOrDefault(j => string.Equals(j.Name, documentName, StringComparison.OrdinalIgnoreCase))
                              ?? jobs.FirstOrDefault(j => j.Submitter == Environment.UserName && (observedJobId == null || j.JobIdentifier == observedJobId));

                    if (job != null)
                    {
                        observedJobId = job.JobIdentifier;

                        if (!job.JobStatus.HasFlag(PrintJobStatus.Spooling) && job.NumberOfPages >= pagesToPrint) return true;
                        if (job.IsCompleted || job.IsDeleted) return true;

                        if (job.JobStatus.HasFlag(PrintJobStatus.Error) || job.JobStatus.HasFlag(PrintJobStatus.Offline))
                            throw new InvalidOperationException($"Print job in error state: {job.JobStatus}");

                        if (job.NumberOfPages > 0 && job.IsCompleted) return true;
                    }
                    else
                    {
                        if (observedJobId != null) return true;
                    }
                }
                catch(Exception ex)
                {
                    Debug.WriteLine($"Exception when waiting for job completion: {ex}");
                }

                Thread.Sleep(500);
            }

            return false;
        }

        public bool MoveSplitFileToPrinter(Guid sourceJobId, string splitPdfName, string targetPrinterName)
        {
            if (string.IsNullOrWhiteSpace(splitPdfName)) throw new ArgumentException(nameof(splitPdfName));
            if (targetPrinterName == null) throw new ArgumentNullException(nameof(targetPrinterName));

            bool changed = false;

            lock (_lock)
            {
                PrinterClass sourcePrinter = null;
                Job sourceJob = null;
                foreach (var p in _printers)
                {
                    var j = p.Jobs.FirstOrDefault(x => x.jobId == sourceJobId);
                    if (j != null)
                    {
                        sourcePrinter = p;
                        sourceJob = j;
                        break;
                    }
                }
                if (sourceJob == null) throw new KeyNotFoundException("Source job not found.");

                var idx = sourceJob.fileNames.FindIndex(f => string.Equals(f, splitPdfName, StringComparison.OrdinalIgnoreCase));
                if (idx < 0) return false;

                var targetName = targetPrinterName.Trim();
                if (string.Equals(sourceJob.printerName ?? string.Empty, targetName, StringComparison.OrdinalIgnoreCase))
                    return false;

                var targetPrinter = _printers.FirstOrDefault(p => string.Equals(p.Name ?? string.Empty, targetName, StringComparison.OrdinalIgnoreCase));
                if (targetPrinter == null)
                {
                    targetPrinter = new PrinterClass { Name = targetName, Status = "Unknown" };
                    _printers.Add(targetPrinter);
                }

                var targetJob = targetPrinter.Jobs.FirstOrDefault(j =>
                    string.Equals(j.orgPdfName ?? string.Empty, sourceJob.orgPdfName ?? string.Empty, StringComparison.OrdinalIgnoreCase));

                if (targetJob == null)
                {
                    targetJob = new Job(targetPrinter.Name, new List<string> { splitPdfName }, sourceJob.Simplex, sourceJob.orgPdfName)
                    {
                        dateTime = DateTime.Now
                    };
                    targetPrinter.Jobs.Add(targetJob);
                }
                else if (!targetJob.fileNames.Any(f => string.Equals(f, splitPdfName, StringComparison.OrdinalIgnoreCase)))
                {
                    targetJob.fileNames.Add(splitPdfName);
                }

                sourceJob.fileNames.RemoveAt(idx);

                if (sourceJob.fileNames.Count == 0 && sourcePrinter != null)
                {
                    sourcePrinter.Jobs.RemoveAll(j => j.jobId == sourceJob.jobId);
                }

                SavePrinterStore();
                changed = true;

                try { ActivityLogger.LogAction("MoveSplitFile", $"Moved '{splitPdfName}' from job {sourceJobId} to printer '{targetPrinter.Name}'"); } catch { }
            }

            if (changed) RaiseJobsChanged();
            return true;
        }

        private bool TryFindJobIdBySplitFile(string splitPdfName, out Guid jobId)
        {
            jobId = Guid.Empty;
            if (string.IsNullOrWhiteSpace(splitPdfName)) return false;

            lock (_lock)
            {
                var job = _printers
                    .SelectMany(p => p.Jobs)
                    .FirstOrDefault(j => j.fileNames != null && j.fileNames.Any(f => string.Equals(f, splitPdfName, StringComparison.OrdinalIgnoreCase)));

                if (job != null)
                {
                    jobId = job.jobId;
                    return true;
                }
            }

            return false;
        }

        private void TryDeleteSplitFileFromDisk(string splitPdfName)
        {
            if (string.IsNullOrWhiteSpace(splitPdfName)) return;

            var jobDirFull = Path.GetFullPath(AppSettings.JobDir);
            var fullPath = Path.GetFullPath(Path.Combine(AppSettings.JobDir, splitPdfName));

            if (!fullPath.StartsWith(jobDirFull, StringComparison.OrdinalIgnoreCase)) return;

            if (File.Exists(fullPath))
            {
                try
                {
                    File.Delete(fullPath);
                }
                catch (Exception ex)
                {
                    try { ActivityLogger.LogAction("AutoDeleteSplitAfterPrintError", $"Failed to delete '{fullPath}': {ex.Message}"); } catch { }
                }
            }
        }
    }
}
