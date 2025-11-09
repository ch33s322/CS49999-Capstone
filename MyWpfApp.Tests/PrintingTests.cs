using MyWpfApp.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace MyWpfApp.Tests
{
    public class PrintingTests
    {
        [Fact]
        public void TestGetAdobeReaderPath()
        {
            // get Adobe Reader path
            var pm = new PrintManager();
            var actualPath = pm.AdobeReaderPath;

            // Output for debugging because can have different paths on different systems
            // should be manually checked and not null
            Debug.WriteLine($"Adobe Reader Path: {actualPath}");
            // Assert
            Assert.True(File.Exists(actualPath));
            Assert.NotNull(actualPath);
        }
        /*
        [Fact]
        public void TestPrintingSinglePagePdf()
        {
            // Arrange
            var pm = new PrintManager();
            var testPdfName = "Information_on_Windows_Protected_Print_Mode.pdf";
            var printerName = "hp LaserJet 3015 PCL 5"; // printer on test system
            var pdfPath = "C:/Users/Austin/source/repos/ch33s322/CS49999-Capstone/MyWpfApp.Tests/Information_on_Windows_Protected_Print_Mode.pdf";
           
            Assert.True(File.Exists(pdfPath), $"Test PDF '{testPdfName}' not found at '{pdfPath}'.");

            // Ensure JobDir exists and copy the test PDF there because PrintManager looks for files under AppSettings.JobDir
            Directory.CreateDirectory(AppSettings.JobDir);
            var destPath = Path.Combine(AppSettings.JobDir, testPdfName);
            File.Copy(pdfPath, destPath, overwrite: true);

            Assert.True(File.Exists(destPath), $"Failed to copy test PDF to JobDir at '{destPath}'.");
            // Act
            var printResult = pm.PrintJob(testPdfName, printerName);

            // Assert
            
            Assert.True(printResult, "Printing the single-page PDF failed.");
        }
        */

    }
}
