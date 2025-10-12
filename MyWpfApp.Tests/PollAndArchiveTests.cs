using MyWpfApp.Model;
using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using Xunit;
using PdfSharpCore.Pdf;

namespace MyWpfApp.Tests
{
    public class PollAndArchiveTests
    {
        private string _inputDir;
        private string _archiveDir;
        private string _outputDir;

        // Use PdfSharpCore to create a PDF file with one page
        private void CreateValidPdf(string path)
        {
            using (var doc = new PdfDocument())
            {
                doc.AddPage();
                doc.Save(path);
            }
        }

        [Fact]
        public void ArchivesAndSplitsNewPdf()
        {
            // Arrange
            _inputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _archiveDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_inputDir);
            Directory.CreateDirectory(_archiveDir);
            Directory.CreateDirectory(_outputDir);

            var testPdf = Path.Combine(_inputDir, "test.pdf");
            var archived = Path.Combine(_archiveDir, "test.pdf");
            var poller = new PollAndArchive(_inputDir, _archiveDir);

            try
            {
                poller.StartWatching(1, _outputDir);
                CreateValidPdf(testPdf);

                // Wait up to 10 seconds (10000ms) for the archive file to appear
                int waited = 0;
                while (!File.Exists(archived) && waited < 10000)
                {
                    Thread.Sleep(100);
                    waited += 100;
                }
                Assert.True(File.Exists(archived), "PDF should be archived.");

                // Wait up to 10 seconds for at least one split file to appear
                string[] splitFiles = Array.Empty<string>();
                waited = 0;
                while (splitFiles.Length == 0 && waited < 10000)
                {
                    splitFiles = Directory.GetFiles(_outputDir, "*.pdf");
                    if (splitFiles.Length == 0)
                    {
                        Thread.Sleep(100);
                        waited += 100;
                    }
                }
                Assert.True(splitFiles.Length > 0, "At least one split PDF should be created.");
            }
            finally
            {
                poller.StopWatching();
                if (Directory.Exists(_inputDir)) Directory.Delete(_inputDir, true);
                if (Directory.Exists(_archiveDir)) Directory.Delete(_archiveDir, true);
                if (Directory.Exists(_outputDir)) Directory.Delete(_outputDir, true);
            }
        }

        [Fact]
        public void DoesNotArchiveOrSplitTwice()
        {
            // Arrange
            _inputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _archiveDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_inputDir);
            Directory.CreateDirectory(_archiveDir);
            Directory.CreateDirectory(_outputDir);

            var testPdf = Path.Combine(_inputDir, "test2.pdf");
            var archived = Path.Combine(_archiveDir, "test2.pdf");
            var poller = new PollAndArchive(_inputDir, _archiveDir);

            try
            {
                poller.StartWatching(1, _outputDir);
                CreateValidPdf(testPdf);

                // Wait up to 10 seconds for the archive file to appear
                int waited = 0;
                while (!File.Exists(archived) && waited < 10000)
                {
                    Thread.Sleep(100);
                    waited += 100;
                }
                Assert.True(File.Exists(archived), "PDF should be archived.");

                // Wait up to 10 seconds for the split file to appear
                string[] splitFiles = Array.Empty<string>();
                waited = 0;
                while (splitFiles.Length == 0 && waited < 10000)
                {
                    splitFiles = Directory.GetFiles(_outputDir, "test2_part_1-1.pdf");
                    if (splitFiles.Length == 0)
                    {
                        Thread.Sleep(100);
                        waited += 100;
                    }
                }
                Assert.Equal(1, splitFiles.Length);

                // Touch the file again (simulate re-creation of PDF)
                File.SetLastWriteTime(testPdf, DateTime.Now);
                Thread.Sleep(1000);

                // Assert again: There should still only be one split for this file
                splitFiles = Directory.GetFiles(_outputDir, "test2_part_1-1.pdf");
                Assert.Equal(1, splitFiles.Length);
            }
            finally
            {
                poller.StopWatching();
                if (Directory.Exists(_inputDir)) Directory.Delete(_inputDir, true);
                if (Directory.Exists(_archiveDir)) Directory.Delete(_archiveDir, true);
                if (Directory.Exists(_outputDir)) Directory.Delete(_outputDir, true);
            }
        }
    }
}