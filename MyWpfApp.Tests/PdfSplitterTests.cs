using MyWpfApp.Model;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyWpfApp.Tests
{
    public class PdfSplitterTests
    {
        /*Testing if all exceptions are properly thrown*/
        [Fact]
        public void TestBadInputPath()
        {
            //create bad path that doesn't exist
            string badPath = "./asdfa.pdf";
            //create tmpOutputDir ;a;sldkfja;sdkfja;sldkfja;lk
            string tmpOutputDir = Path.GetTempPath();
            //create pdfsplitter object
            var pdfSplitter = new PdfSplitter();
            //test if error is thrown properly
            Assert.Throws<FileNotFoundException>(() =>
                pdfSplitter.SplitPdf(badPath, tmpOutputDir, 2)
            );
        }

        [Fact]
        public void TestBadOutputPath()
        {
            //create temporary test pd
            string tempFile = Path.Combine(Path.GetTempPath(), "test.pdf");
            //Write to file
            File.WriteAllText(tempFile, "temporary text that we write to the file after we make it");
            //create pdf splitter object
            var pdfSplitter = new PdfSplitter();
            //create badouput dir
            string badOutputPath = "../nonexistentDirectory";
            //test if correct error is thrown
            try
            {
                Assert.Throws<DirectoryNotFoundException>(
                    () => pdfSplitter.SplitPdf(tempFile, badOutputPath, 2)
                 );
            }
            finally
            {
                //delete temp file to clean up
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void TestInvalidMaxPage()
        {
            //create test pdf at a valid path
            string tempFilePath = Path.Combine(Path.GetTempPath(), "test.pdf");
            //Create test pdf with some dummy text
            File.WriteAllText(tempFilePath, "temporary text that we write to the file after we make it");
            //make output path current directory
            string outputPath = "./";
            //create pdf splitter object
            var pdfSplitter = new PdfSplitter();
            try
            {
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => pdfSplitter.SplitPdf(tempFilePath, outputPath, 0)
                );
            }
            finally
            {
                //delete temp file to clean up
                File.Delete(tempFilePath);
            }
        }

        /*Functional tests*/
        //test if output file or files are even created
        [Fact]
        public void SampleCaseTestPdfSplitterFuncitonality()
        {
            //create test pdf
            string path = Path.Combine(Path.GetTempPath(), $"test.pdf");
            using (var pdf = new PdfDocument())
            {
                for (int i = 0; i < 40000; i++)
                {
                    pdf.AddPage(new PdfPage());
                }
                pdf.Save(path);
            }
            //check if test pdf exists
            Assert.True(File.Exists(path));

            //create temporary output directory
            string outputDir = Path.Combine(Path.GetTempPath(), "/output");

            //create the directory
            Directory.CreateDirectory(outputDir);

            //test if output directory exists
            Assert.True(Directory.Exists(outputDir));

            //split it into 1000 page increments
            var pdfSplitter = new PdfSplitter();
            List<string> fileNames = pdfSplitter.SplitPdf(path, outputDir, 1000);

            //check if there are the right amount of files in output directory
            string[] files = Directory.GetFiles(outputDir);
            Assert.Equal(40000, files.Length*1000);

            //Check if all file names are the same
            List<string> fileNames2 = new List<string>();
            int j = 0;
            foreach (string filePath in files)
            {
                fileNames2.Add(Path.GetFileName(filePath));
            }
            fileNames2.Sort();
            fileNames.Sort();
            Assert.Equal(fileNames2, fileNames);

            //check the total sum of all the pdf files in the folder and compare it to original
            //check the total sum of all pages in the output PDFs
            int totalPages = 0;
            foreach (string filePath in files)
            {
                using (PdfDocument outputPdf = PdfReader.Open(filePath, PdfDocumentOpenMode.Import))
                {
                    totalPages += outputPdf.PageCount;
                }
            }
            //compare total pages with original PDF
            using (PdfDocument originalPdf = PdfReader.Open(path, PdfDocumentOpenMode.Import))
            {
                Assert.Equal(originalPdf.PageCount, totalPages);
            }

            //cleanup
            File.Delete(path);
            Directory.Delete(outputDir, true);
            Assert.False(File.Exists(path));
            Assert.False(Directory.Exists(outputDir));


        }
        /*Testing if split pdf sizes work from sizes of 1 pages up to 2000 page splits*/
        //test output list
        //test output file names
        //ensure when split that output files when summed have same number of pages as input files

    }
}
