using MyWpfApp.Model;
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
using PrinterClass = Printer.Model.Printer;

namespace MyWpfApp.Model
{
    public class PrintManager
    {
        private readonly object _lock = new object();

        // In-memory store of Printer objects (each Printer has a List<Job> Jobs)
        private List<PrinterClass> _printers = new List<PrinterClass>();

        // name used for unassigned jobs
        private const string UnassignedPrinterName = "";

        public string AdobeReaderPath { get; private set; }
        public PrintManager()
        {
            AppSettings.EnsureDirectoriesExist();
            LoadPrinterStore();
            EnsureUnassignedPrinterExists();
            AdobeReaderPath = GetAdobeReaderPath();
        }

        private void EnsureUnassignedPrinterExists()
        {
            lock (_lock)
            {
                if (!_printers.Any(p => string.Equals(p.Name ?? string.Empty, UnassignedPrinterName, StringComparison.OrdinalIgnoreCase)))
                {
                    var unassigned = new PrinterClass { Name = UnassignedPrinterName, Status = "Unassigned" };
                    _printers.Add(unassigned);
                    SavePrinterStore();
                }
            }
        }


        // queues a job 
        public void QueueJob(Job job)
        {
            if (job == null) throw new ArgumentNullException(nameof(job));

            lock (_lock)
            {
                var targetName = string.IsNullOrWhiteSpace(job.printerName) ? UnassignedPrinterName : job.printerName;
                var target = _printers.FirstOrDefault(p => string.Equals(p.Name, targetName, StringComparison.OrdinalIgnoreCase));

                if (target == null)
                {
                    // create printer entry if not present
                    target = new PrinterClass { Name = targetName, Status = "Unknown" };
                    _printers.Add(target);
                }

                // avoid duplicate job ids
                if (!target.Jobs.Any(j => j.jobId == job.jobId))
                {
                    job.printerName = target.Name; // keep job consistent
                    target.Jobs.Add(job);
                    SavePrinterStore();
                }
            }
        }

        // release job
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

                if (changed) SavePrinterStore();
            }
        }

        // set job to simplex/duplex
        public void SetJobSimplexFlag(Guid jobId, bool flag)
        {
            lock (_lock)
            {
                var job = _printers.SelectMany(p => p.Jobs).FirstOrDefault(j => j.jobId == jobId);
                if (job == null) throw new KeyNotFoundException("Job not found");
                job.Simplex = flag;
                SavePrinterStore();
            }
        }

        // sets a job to a printer (moves job object between Printer.Jobs lists)
        public void SetJobPrinter(Guid jobId, string printerName)
        {
            if (printerName == null) throw new ArgumentNullException(nameof(printerName));

            lock (_lock)
            {
                    // find and remove job from current printer (if any)
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

                // if job was not found, we cannot move it — caller should QueueJob first
                if (job == null) throw new KeyNotFoundException("Job not found; queue the job before assigning");

                // determine target printer
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
        }

        // adds printer to printer set (and persists the Printer including its Jobs list)
        public bool AddPrinter(string PrinterName)
        {
            if (string.IsNullOrWhiteSpace(PrinterName)) throw new ArgumentException(nameof(PrinterName));

            // ensure directory exists
            if (!Directory.Exists(AppSettings.PrinterDir))
            {
                Directory.CreateDirectory(AppSettings.PrinterDir);
            }

            // create printer directory if missing 
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
                    return true;
                }
            }

            return false;
        }

        // removes printer and moves its jobs to the unassigned printer
        public PrinterClass RemovePrinter(string printerName)
        {
            if (string.IsNullOrWhiteSpace(printerName)) throw new ArgumentException(nameof(printerName));

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

                // move jobs
                foreach (var j in entry.Jobs)
                {
                    j.printerName = unassigned.Name;
                    if (!unassigned.Jobs.Any(x => x.jobId == j.jobId))
                    {
                        unassigned.Jobs.Add(j);
                    }
                }

                _printers.Remove(entry);
                SavePrinterStore();

                // remove directory if exists
                try
                {
                    string printerPath = Path.Combine(AppSettings.PrinterDir, printerName);
                    if (Directory.Exists(printerPath)) Directory.Delete(printerPath, true);
                }
                catch
                {
                    // ignore IO errors
                }

                return entry;
            }
        }

        // used to get all jobs (flattened)
        public List<Job> GetJobs()
        {
            lock (_lock)
            {
                return _printers.SelectMany(p => p.Jobs).ToList();
            }
        }

        // get jobs for a printer, returns unsorted list of jobs for printer
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

        // get all printers
        public List<string> getAllPrinters()
        {
            lock (_lock)
            {
                return _printers.Select(p => p.Name).ToList();
            }
        }

        // --- Persistence DTO & helpers ---
        // Persist a lightweight DTO (so we don't try to serialize events / bindings)
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

                        // convert DTOs to PrinterClass instances
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
                catch
                {
                    // swallow IO errors; consider logging in real app
                }
            }
        }

        private string GetAdobeReaderPath()
        {
            // Attempt to find Adobe Reader executable path from registry
            try
            {
                // Check 32-bit registry view
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\AcroRd32.exe"))
                {
                    if (key != null)
                    {
                        var path = key.GetValue("") as string;
                        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                        {
                            return path;
                        }
                    }
                }

                // Check 64-bit registry view
                using (var key = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64)
                    .OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\AcroRd32.exe"))
                {
                    if (key != null)
                    {
                        var path = key.GetValue("") as string;
                        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                        {
                            return path;
                        }
                    }
                }

                // Check WOW6432Node for 32-bit Adobe Reader on 64-bit Windows
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths\AcroRd32.exe"))
                {
                    if (key != null)
                    {
                        var path = key.GetValue("") as string;
                        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                        {
                            return path;
                        }
                    }
                }

                // check path for full Acrobat if reader not found
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\Acrobat.exe"))
                {
                    if (key != null)
                    {
                        var path = key.GetValue("") as string;
                        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                        {
                            return path;
                        }
                    }
                }
            }
            catch
            {
                // ignore registry access errors
            }

            return null;
        }

        // -- Printing --

        public bool PrintJob(string pdfName, string printerName)
        {
            // validating job and adobe reader path
            if (string.IsNullOrWhiteSpace(pdfName)) throw new ArgumentException(nameof(pdfName));
            
            var inputPdfPath = Path.Combine(AppSettings.JobDir, pdfName);
            if (!File.Exists(inputPdfPath)) throw new FileNotFoundException("PDF file not found in JobWell.", inputPdfPath);
            if (string.IsNullOrWhiteSpace(AdobeReaderPath)) throw new InvalidOperationException("Adobe Reader path not set.");
            if (!File.Exists(AdobeReaderPath)) throw new FileNotFoundException("Adobe Reader executable not found.", AdobeReaderPath);

            // command line arguments for adobe reader
            string arguments = $"/t \"{inputPdfPath}\" \"{printerName}\"";

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = AdobeReaderPath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
            };

            int pageCount = 0;
           using (PdfDocument input = PdfReader.Open(inputPdfPath, PdfDocumentOpenMode.Import))
            {
                pageCount = input.PageCount;
            }

            Debug.WriteLine("starting process for print");
            System.Diagnostics.Process proc = null;
            try
            {
                proc = System.Diagnostics.Process.Start(psi);
                if (proc == null)
                    throw new InvalidOperationException("Failed to start Adobe Reader process.");

                TimeSpan timeoutMs = TimeSpan.FromMinutes(30); // 30 minute timeout for print job completion

                bool completed;
                try
                {
                    completed = WaitForPrintJobCompletion(printerName, pdfName, pageCount, timeoutMs);
                }
                catch
                {
                    // propagate any exceptions from the spooler waiting logic (e.g. job error states)
                    try { if (proc != null && !proc.HasExited) proc.Kill(); Debug.WriteLine("process timed out"); } catch { }
                    throw;
                }

                // timed out waiting for spooler -> do not throw, return false per requirement
                if (!completed)
                {
                    try { if (proc != null && !proc.HasExited) proc.Kill(); } catch { }
                    return false;
                }

                // If process has exited, verify exit code; otherwise treat as success since we observed the print job
                try
                {
                    if (proc.HasExited)
                    {
                        if (proc.ExitCode != 0)
                            throw new InvalidOperationException($"Adobe Reader exited with code {proc.ExitCode} when printing.");
                    }
                }
                catch (InvalidOperationException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // Any unexpected error when querying the process state should be surfaced as an exception
                    throw new InvalidOperationException("Failed to verify Adobe Reader process state.", ex);
                }

                return true;
            }
            finally
            {
                // Best-effort cleanup: if process still running, attempt to kill it.
                try { if (proc != null && !proc.HasExited) proc.Kill(); } catch { }
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
                return false; // printer not available
            }

            var sw = Stopwatch.StartNew();
            int? observedJobId = null;

            while (sw.Elapsed < timeout)
            {
                try
                {
                    queue.Refresh();
                    var jobs = queue.GetPrintJobInfoCollection().Cast<PrintSystemJobInfo>().ToList();

                    // try to find the job by name first, then fallback to submitter + time heuristics
                    var job = jobs.FirstOrDefault(j => string.Equals(j.Name, documentName, StringComparison.OrdinalIgnoreCase))
                              ?? jobs.FirstOrDefault(j => j.Submitter == Environment.UserName && (observedJobId == null || j.JobIdentifier == observedJobId));

                    if (job != null)
                    {
                        observedJobId = job.JobIdentifier;

                        // job still in spooler; finish when pages reach pagesToPrint
                        //if (job.NumberOfPages >= pagesToPrint) return true;
                        if (!job.JobStatus.HasFlag(PrintJobStatus.Spooling) && job.NumberOfPages >= pagesToPrint) return true;

                        // job finished successfully (removed from spooler) or reported completed
                        if (job.IsCompleted || job.IsDeleted) return true;

                        // Some environments set JobStatus flags; check for error states
                        if (job.JobStatus.HasFlag(PrintJobStatus.Error) || job.JobStatus.HasFlag(PrintJobStatus.Offline))
                            throw new InvalidOperationException($"Print job in error state: {job.JobStatus}");

                        // number of pages may be available as an indicator
                        if (job.NumberOfPages > 0 && job.IsCompleted) return true;
                    }
                    else
                    {
                        // if we previously saw a job but now it is gone, assume completion
                        if (observedJobId != null) return true;
                    }
                }
                catch
                {
                    // transient spooler errors — retry until timeout
                }

                Thread.Sleep(500);
            }

            return false; // timed out
        }
    }
}
