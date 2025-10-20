using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyWpfApp.Model;
using PdfSharpCore.Pdf;
using Xunit;

namespace MyWpfApp.Tests
{
    public class PollAndArchiveIntegrationTests : IDisposable
    {
        // Test setup
        private readonly string _inputDir;
        private readonly string _archiveDir;
        private readonly string _outputDir;

        public PollAndArchiveIntegrationTests()
        {
            _inputDir = Path.Combine(Path.GetTempPath(), "pollandarchive_input_" + Guid.NewGuid());
            _archiveDir = Path.Combine(Path.GetTempPath(), "pollandarchive_archive_" + Guid.NewGuid());
            _outputDir = Path.Combine(Path.GetTempPath(), "pollandarchive_output_" + Guid.NewGuid());

            Directory.CreateDirectory(_inputDir);
            Directory.CreateDirectory(_archiveDir);
            Directory.CreateDirectory(_outputDir);
        }

        public void Dispose()
        {
            // Delete test dirs
            TryDelete(_inputDir);
            TryDelete(_archiveDir);
            TryDelete(_outputDir);
        }

        // For delketing test dirs
        private void TryDelete(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); }
            catch {}
        }

        // Create small PDF for tests
        private void CreateValidPdf(string path)
        {
            using (var doc = new PdfDocument())
            {
                doc.AddPage();
                // Ensure directory exists for the temporary write
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                doc.Save(path);
            }
        }

        // Wait up to timeoutMs for a file to exist
        // Returs true if file exists in dir
        private static async Task<bool> WaitForFileAsync(string path, int timeoutMs = 10000, int pollMs = 100)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (File.Exists(path)) return true;
                await Task.Delay(pollMs).ConfigureAwait(false);
            }
            return false;
        }

        // Wait up to timeoutMs for PDF file in dir
        private static async Task<string[]> WaitForFilesAsync(string directory, string searchPattern = "*.pdf", int timeoutMs = 10000, int pollMs = 100)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (Directory.Exists(directory))
                {
                    var files = Directory.GetFiles(directory, searchPattern);
                    if (files.Length > 0) return files;
                }
                await Task.Delay(pollMs).ConfigureAwait(false);
            }
            return Array.Empty<string>();
        }

        [Fact]
        public async Task ArchivesAndSplitsNewPdf()
        {
            var poller = new PollAndArchive(_inputDir, _archiveDir, _outputDir);
            try
            {
                poller.StartWatching();

                // Write to temp outside watched dir, then move in
                var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".pdf.part");
                CreateValidPdf(temp);

                var target = Path.Combine(_inputDir, "test.pdf");
                File.Move(temp, target);

                // Check for archived copy inside
                var archived = Path.Combine(_archiveDir, "test.pdf");
                var archivedAppeared = await WaitForFileAsync(archived, timeoutMs: 10000);
                Assert.True(archivedAppeared, $"Expected archived file to appear: {archived}");

                // Check for split PDF (at least 1) in output dir
                var splits = await WaitForFilesAsync(_outputDir, "*.pdf", timeoutMs: 10000);
                var outputFile = Path.Combine(_outputDir, "test.pdf");
                Assert.True(File.Exists(outputFile), "Expected the PDF to be moved to the output directory.");
            }
            finally
            {
                poller.Dispose();
            }
        }

        [Fact]
        public async Task AtomicCreateTriggersProcessing()
        {
            var poller = new PollAndArchive(_inputDir, _archiveDir, _outputDir);
            try
            {
                poller.StartWatching();

                // Write a PDF to a temp subdir and then move into watched dir
                var tmpDir = Path.Combine(Path.GetTempPath(), "pa_test_tmp_" + Guid.NewGuid());
                Directory.CreateDirectory(tmpDir);
                var tmpFile = Path.Combine(tmpDir, "testpollandarchive.pdf");
                CreateValidPdf(tmpFile);

                var target = Path.Combine(_inputDir, "testpollandarchive.pdf");
                File.Move(tmpFile, target);

                // Assert archive exists
                var archived = Path.Combine(_archiveDir, "testpollandarchive.pdf");
                Assert.True(await WaitForFileAsync(archived, 10000), "Archived PDF did not appear within timeout.");

                var outputFile = Path.Combine(_outputDir, "testpollandarchive.pdf");
                Assert.True(File.Exists(outputFile), "PDF was not moved to the output directory.");
            }
            finally
            {
                poller.Dispose();
            }
        }

        [Fact]
        public async Task ProcessesMultipleFilesConcurrently()
        {
            // Arrange
            var poller = new PollAndArchive(_inputDir, _archiveDir, _outputDir);
            try
            {
                poller.StartWatching();

                const int fileCount = 3;
                var fileNames = Enumerable.Range(1, fileCount).Select(i => $"multi_{i}.pdf").ToArray();

                // Checking if multiple files can be handled at once
                foreach (var name in fileNames)
                {
                    var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".pdf.part");
                    CreateValidPdf(tmp);
                    var dest = Path.Combine(_inputDir, name);
                    File.Move(tmp, dest);
                }

                // Wait longer for multiple files
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var succeededAll = false;
                while (sw.ElapsedMilliseconds < 15000)
                {
                    var allArchived = fileNames.All(n => File.Exists(Path.Combine(_archiveDir, n)));
                    if (allArchived)
                    {
                        succeededAll = true;
                        break;
                    }
                    await Task.Delay(200).ConfigureAwait(false);
                }
                Assert.True(succeededAll, "Not all files were archived in time.");

                // Verify that for each file at least one split PDF exists in output dir
                var outputs = Directory.Exists(_outputDir) ? Directory.GetFiles(_outputDir, "*.pdf") : Array.Empty<string>();
                Assert.Equal(fileCount, outputs.Length);
            }
            finally
            {
                poller.Dispose();
            }
        }
    }
}
