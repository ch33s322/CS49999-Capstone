using MyWpfApp.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyWpfApp.Tests
{
    public class PrinterTests
    {
        [Fact]
        public void TestBadPrinterCreation()
        {
            var storePath = AppSettings.PrinterStoreFile;
            var backupPath = storePath + ".bak";
            bool hadBackup = false;

            try
            {
                // Backup existing persisted store if present
                if (File.Exists(storePath))
                {
                    File.Copy(storePath, backupPath, overwrite: true);
                    hadBackup = true;
                }

                // Create PrintManager (constructor may read/create the store) and call AddPrinter with null
                var pm = new PrintManager();

                // Ensure AddPrinter throws for null/whitespace input
                Assert.Throws<ArgumentException>(() => pm.AddPrinter(null));
            }
            finally
            {
                // Restore original persisted store state so test does not pollute permanent storage
                try
                {
                    if (hadBackup)
                    {
                        // restore original file
                        File.Copy(backupPath, storePath, overwrite: true);
                        File.Delete(backupPath);
                    }
                    else
                    {
                        // no original file — ensure test-created store is removed
                        if (File.Exists(storePath))
                            File.Delete(storePath);
                    }
                }
                catch
                {
                    // suppress exceptions during cleanup to avoid masking test results;
                    // in CI you might want to log failures here.
                }
            }
        }

        [Fact]
        public void TestPrinterCreation()
        {
            // Validate model behavior first
            var printerName = "TestPrinter";
            var printerStatus = "Idle";

            var printer = new Printer.Model.Printer
            {
                Name = printerName,
                Status = printerStatus
            };

            Assert.Equal(printerName, printer.Name);
            Assert.Equal(printerStatus, printer.Status);
            Assert.Empty(printer.Jobs);

            // Now test PrintManager.AddPrinter without polluting permanent store
            var storePath = AppSettings.PrinterStoreFile;
            var backupPath = storePath + ".bak";
            bool hadBackup = false;

            try
            {
                if (File.Exists(storePath))
                {
                    File.Copy(storePath, backupPath, overwrite: true);
                    hadBackup = true;
                }

                var pm = new PrintManager();
                pm.AddPrinter(printerName);

                var printers = pm.getAllPrinters();
                Assert.Contains(printerName, printers);
            }
            finally
            {
                // cleanup: restore persisted store and remove created printer directory
                try
                {
                    if (hadBackup)
                    {
                        File.Copy(backupPath, storePath, overwrite: true);
                        File.Delete(backupPath);
                    }
                    else
                    {
                        if (File.Exists(storePath))
                            File.Delete(storePath);
                    }

                    var printerDir = Path.Combine(AppSettings.PrinterDir, printerName);
                    if (Directory.Exists(printerDir))
                        Directory.Delete(printerDir, true);
                }
                catch
                {
                    // suppress cleanup exceptions
                }
            }
        }

        [Fact]
        public void TestPrinterToString()
        {
            var printer = new Printer.Model.Printer
            {
                Name = "OfficePrinter",
                Status = "Ready"
            };
            printer.Jobs.Add(new Job { jobId = Guid.NewGuid(), orgPdfName = "Test Job 1" });
            printer.Jobs.Add(new Job { jobId = Guid.NewGuid(), orgPdfName = "Test Job 2" });
            var expectedString = "Printer Name: OfficePrinter, Status: Ready, Jobs Count: 2";
            Assert.Equal(expectedString, printer.ToString());
        }
        
        [Fact]
        public void TestAssignJobToPrinter()
        {
            var printer = new Printer.Model.Printer
            {
                Name = "OfficePrinter",
                Status = "Ready"
            };
            var job = new Job
            {
                orgPdfName = "Document.pdf",
                printerName = printer.Name,
                fileNames = new List<string> { "Document_part1.pdf", "Document_part2.pdf" },
                Simplex = false
            };
            printer.Jobs.Add(job);
            Assert.Single(printer.Jobs);
            Assert.Equal("Document.pdf", printer.Jobs[0].orgPdfName);
            Assert.Equal("OfficePrinter", printer.Jobs[0].printerName);
            Assert.False(printer.Jobs[0].Simplex);

            var job2 = new Job
            {
                orgPdfName = "AnotherDoc.pdf",
                printerName = printer.Name,
                fileNames = new List<string> { "AnotherDoc_part1.pdf" },
                Simplex = true
            };
            printer.Jobs.Add(job2);
            Assert.Equal(2, printer.Jobs.Count);
            Assert.Equal("AnotherDoc.pdf", printer.Jobs[1].orgPdfName);
            Assert.True(printer.Jobs[1].Simplex);
        }

        [Fact]
        public void TestAssignJobToPrinter_QueueJob()
        {
            var storePath = AppSettings.PrinterStoreFile;
            var backupPath = storePath + ".bak";
            bool hadBackup = false;
            var targetPrinterName = "TargetPrinter";

            try
            {
                // Backup persisted store if exists
                if (File.Exists(storePath))
                {
                    File.Copy(storePath, backupPath, overwrite: true);
                    hadBackup = true;
                }

                var pm = new PrintManager();

                // Create a job with the desired printer name and queue it
                var job = new Job
                {
                    orgPdfName = "MoveMe.pdf",
                    printerName = targetPrinterName, // specify target printer before queuing
                    fileNames = new List<string> { "MoveMe_part1.pdf" },
                    Simplex = true
                };

                pm.QueueJob(job);

                // Verify job placed directly on the target printer
                var targetJobs = pm.GetJobsForPrinter(targetPrinterName);
                Assert.Contains(targetJobs, j => j.jobId == job.jobId && j.printerName == targetPrinterName);

                // Ensure it is not in the unassigned list
                var unassignedJobs = pm.GetJobsForPrinter(string.Empty);
                Assert.DoesNotContain(unassignedJobs, j => j.jobId == job.jobId);
            }
            finally
            {
                // cleanup persisted store and remove created printer directory
                try
                {
                    if (hadBackup)
                    {
                        File.Copy(backupPath, storePath, overwrite: true);
                        File.Delete(backupPath);
                    }
                    else
                    {
                        if (File.Exists(storePath))
                            File.Delete(storePath);
                    }

                    var printerDir = Path.Combine(AppSettings.PrinterDir, targetPrinterName);
                    if (Directory.Exists(printerDir))
                        Directory.Delete(printerDir, true);
                }
                catch
                {
                    // suppress cleanup exceptions
                }
            }
        }
    }
}
