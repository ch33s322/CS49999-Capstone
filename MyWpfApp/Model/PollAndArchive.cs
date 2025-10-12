using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace MyWpfApp.Model
{
    public class PollAndArchive : IDisposable
    {
        private readonly string _inputDirectory;
        private readonly string _archiveDirectory;
        private readonly PdfSplitter _pdfSplitter;
        private readonly FileSystemWatcher _watcher;
        private readonly CancellationTokenSource _cts;

        // Poll input directory for new PDF file, archive it, pass it to PdfSplitter, log actions/errors
        public PollAndArchive(string inputDirectory, string archiveDirectory)
        {
            // Checking if input and archive directories exist
            if (!Directory.Exists(inputDirectory))
                throw new DirectoryNotFoundException($"Input directory not found: {inputDirectory}");
            if (!Directory.Exists(archiveDirectory))
                throw new DirectoryNotFoundException($"Archive directory not found: {archiveDirectory}");

            _inputDirectory = inputDirectory;
            _archiveDirectory = archiveDirectory;
            _pdfSplitter = new PdfSplitter();
            _cts = new CancellationTokenSource();

            _watcher = new FileSystemWatcher(_inputDirectory, "*.pdf")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime
            };
            _watcher.Created += OnPdfCreated;
        }

        // These are necessary for now since maxPages and the temp outputDirectory aren't global values
        // Once we have these configurable as settings we can remove these variables
        private int _maxPages;
        private string _outputDirectory;

        public void StartWatching(int maxPages, string outputDirectory)
        {
            _maxPages = maxPages;
            _outputDirectory = outputDirectory;
            _watcher.EnableRaisingEvents = true;
            Debug.WriteLine("Started watching for new PDF files.");
        }

        // When new PDF file is found in input directory
        private void OnPdfCreated(object sender, FileSystemEventArgs e)
        {
            // Run processing in a background task to avoid blocking our FileSystemWatcher's thread
            Task.Run(() =>
            {
                try
                {
                    // Ensure that file is fully written and not locked
                    for (int i = 0; i < 10; i++)
                    {
                        try
                        {
                            using (FileStream stream = File.Open(e.FullPath, FileMode.Open, FileAccess.Read, FileShare.None))
                            {
                                break;
                            }
                        }
                        catch (IOException)
                        {
                            // Waiut and retry every 0.2s
                            Thread.Sleep(200);
                        }
                    }

                    // Archive original PDF if it doesn't yet exist
                    string archivePath = Path.Combine(_archiveDirectory, Path.GetFileName(e.FullPath));
                    if (!File.Exists(archivePath))
                    {
                        File.Copy(e.FullPath, archivePath);
                        Debug.WriteLine($"Successfully archived PDF: {e.FullPath} -> {archivePath}");
                    }
                    else
                    {
                        Debug.WriteLine($"Archive already contains: {archivePath}");
                    }

                    _pdfSplitter.SplitPdf(e.FullPath, _outputDirectory, _maxPages);
                    Debug.WriteLine($"Split PDF: {e.FullPath} into {_outputDirectory}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error processing PDF '{e.FullPath}': {ex}");
                }
            });
        }

        // In case watching needs to be stopped
        public void StopWatching()
        {
            _watcher.EnableRaisingEvents = false;
            _cts.Cancel();
            Debug.WriteLine("Stopped watching for new PDF files.");
        }

        public void Dispose()
        {
            StopWatching();
            _watcher.Dispose();
            _cts.Dispose();
        }
    }
}