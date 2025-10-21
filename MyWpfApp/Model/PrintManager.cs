using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using PrinterClass = Printer.Model.Printer;
using Printer.ViewModel;
using MyWpfApp.Model;

namespace MyWpfApp.Model
{
    public class PrintManager
    {
        private readonly object _lock = new object();

        // In-memory store of Printer objects (each Printer has a List<Job> Jobs)
        private List<PrinterClass> _printers = new List<PrinterClass>();

        // Special name used for unassigned jobs
        private const string UnassignedPrinterName = "";

        public PrintManager()
        {
            AppSettings.EnsureDirectoriesExist();
            LoadPrinterStore();
            EnsureUnassignedPrinterExists();
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

        // --- Public API ---

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
        public void AddPrinter(string PrinterName)
        {
            if (string.IsNullOrWhiteSpace(PrinterName)) throw new ArgumentException(nameof(PrinterName));

            // ensure directory exists
            if (!Directory.Exists(AppSettings.PrinterDir))
            {
                Directory.CreateDirectory(AppSettings.PrinterDir);
            }

            // create printer directory if missing (keeps behavior from earlier)
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
                }
            }
        }

        // removes printer and moves its jobs to the unassigned printer
        public void RemovePrinter(string printerName)
        {
            if (string.IsNullOrWhiteSpace(printerName)) throw new ArgumentException(nameof(printerName));

            lock (_lock)
            {
                var entry = _printers.FirstOrDefault(p => string.Equals(p.Name, printerName, StringComparison.OrdinalIgnoreCase));
                if (entry == null) return;

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
    }
}
