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
        private readonly string _outputDirectory;
        private readonly FileSystemWatcher _watcher;
        private readonly CancellationTokenSource _cts;

        // Poll input directory for new PDF file, archive it, pass it to PdfSplitter, log actions/errors
        public PollAndArchive(string inputDirectory, string archiveDirectory, string outputDirectory)
        {
            // Checking if input and archive directories exist
            if (!Directory.Exists(inputDirectory))
                throw new DirectoryNotFoundException($"Input directory not found: {inputDirectory}");
            if (!Directory.Exists(archiveDirectory))
                throw new DirectoryNotFoundException($"Archive directory not found: {archiveDirectory}");

            _inputDirectory = inputDirectory;
            _archiveDirectory = archiveDirectory;
            _outputDirectory = outputDirectory;
            _cts = new CancellationTokenSource();

            _watcher = new FileSystemWatcher(_inputDirectory, "*.pdf")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime
            };
            _watcher.Created += OnPdfCreated;
        }

        // These are necessary for now since maxPages and the temp outputDirectory aren't global values
        // Once we have these configurable as settings we can remove these variables
        public void StartWatching()
        {
            _watcher.EnableRaisingEvents = true;
            Debug.WriteLine("Started watching for new PDF files.");
        }

        // When new PDF file is found in input directory
        private void OnPdfCreated(object sender, FileSystemEventArgs e)
        {
            _ = ProcessFileAsync(e.FullPath, _cts.Token);
        }

        private async Task ProcessFileAsync(string fullPath, CancellationToken token)
        {
            try
            {
                const int maxAttempts = 10;
                const int delay = 1000;

                var ready = false;
                for (int i = 0; i < maxAttempts; i++)
                {
                    token.ThrowIfCancellationRequested();

                    try
                    {
                        // Tryy opening file to see if it's been fully written to directory
                        using (var fs = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.None))
                        {
                            // Double checking that file has content
                            if (fs.Length > 0)
                            {
                                ready = true;
                                break;
                            }
                        }
                    }
                    catch (IOException)
                    {
                        // File is not written yet
                        Debug.WriteLine($"File not yet written: {fullPath}");
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Permissions error
                        Debug.WriteLine($"Insufficient privileges for: {fullPath}");
                    }

                    // Delay task for 1000ms (1s)
                    await Task.Delay(delay, token).ConfigureAwait(false);
                }

                if (!ready)
                {
                    Debug.WriteLine($"Timed out waiting for file to be ready: {fullPath}");
                    return;
                }

                token.ThrowIfCancellationRequested();

                // Archive original PDF if it doesn't yet exist
                string archivePath = Path.Combine(_archiveDirectory, Path.GetFileName(fullPath));
                if (!File.Exists(archivePath))
                {
                    // Copy (using another thread) the original PDF to the archive directory
                    await Task.Run(() => File.Copy(fullPath, archivePath, overwrite: false), token).ConfigureAwait(false);
                    Debug.WriteLine($"Successfully archived PDF: {fullPath} -> {archivePath}");
                }
                else
                {
                    Debug.WriteLine($"Archive already contains: {archivePath}");
                }

                token.ThrowIfCancellationRequested();

                // Move PDF to output directory
                string outputPath = Path.Combine(_outputDirectory, Path.GetFileName(fullPath));
                if (!File.Exists(outputPath))
                {
                    await Task.Run(() => File.Move(fullPath, outputPath), token).ConfigureAwait(false);
                    Debug.WriteLine($"Moved PDF: {fullPath} -> {outputPath}");
                }
                else
                {
                    Debug.WriteLine($"Output already contains: {outputPath}");
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine($"Processing canceled for {fullPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing PDF '{fullPath}': {ex}");
            }
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
            _watcher.Created -= OnPdfCreated;
            _watcher.Dispose();
            _cts.Dispose();
        }
    }
}