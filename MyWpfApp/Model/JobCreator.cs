using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace MyWpfApp.Model
{
    public class JobCreator
    {
        //reference to pdf splitter
        private readonly PdfSplitter _pdfSplitter;

        // Tracks in-progress split tasks by absolute PDF path so the same PDF isn't split concurrently.
        // Key uses full path to avoid duplicates across relative names.
        private static readonly ConcurrentDictionary<string, Task<Job>> _inProgressSplits = new ConcurrentDictionary<string, Task<Job>>(StringComparer.OrdinalIgnoreCase);

        //constructor
        public JobCreator(PdfSplitter pdfSplitter)
        {
            _pdfSplitter = pdfSplitter;
        }

        public Task<Job> MakeJobAsync(string printerName, string pdfName, bool simplex)
        {
            if (string.IsNullOrWhiteSpace(pdfName)) throw new ArgumentException(nameof(pdfName));

            // normalize to absolute path inside JobWell — this is the key used to dedupe concurrent requests
            var inputPdfPath = Path.Combine(AppSettings.Instance.JobWell, pdfName);
            var key = Path.GetFullPath(inputPdfPath);

            // If a split for the same PDF is already running, return the existing task so callers wait on the same work.
            return _inProgressSplits.GetOrAdd(key, _ => CreateSplitTaskAsync(printerName, pdfName, simplex, key));
        }

        private Task<Job> CreateSplitTaskAsync(string printerName, string pdfName, bool simplex, string key)
        {
            // Run the work on a background thread.
            var task = Task.Run(async () =>
            {
                try
                {
                    var inputPdfPath = Path.Combine(AppSettings.Instance.JobWell, pdfName);
                    if (!File.Exists(inputPdfPath))
                    {
                        throw new FileNotFoundException("Pdf not found in JobWell", inputPdfPath);
                    }

                    if (!Directory.Exists(AppSettings.Instance.JobDir))
                    {
                        Directory.CreateDirectory(AppSettings.Instance.JobDir);
                    }

                    // perform split on a threadpool thread
                    var splitFiles = await Task.Run(() => _pdfSplitter.SplitPdf(inputPdfPath, AppSettings.Instance.JobDir, AppSettings.Instance.MaxPages)).ConfigureAwait(false);

                    var job = new Job(printerName, splitFiles, simplex, pdfName);
                    return job;
                }
                finally
                {
                    // Ensure the in-progress entry is removed when the task completes (success or failure).
                    // Use discard for the out parameter to avoid declaring an unused variable.
                    await Task.Run(() =>
                    {
                        _inProgressSplits.TryRemove(key, out _);
                    });
                }
            });

            return task;
        }

        public void DeletePdfFromJobWell(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return;

            //build full path in JobWell
            var fullPath = Path.Combine(AppSettings.Instance.JobWell, fileName);

            //ensure the file is inside the JobWell directory
            if (!fullPath.StartsWith(AppSettings.Instance.JobWell, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Cannot delete files outside the JobWell directory.");

            if (File.Exists(fullPath))
            {
                try
                {
                    File.Delete(fullPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to delete file '{fullPath}': {ex.Message}");
                }
            }
        }
    }
}
