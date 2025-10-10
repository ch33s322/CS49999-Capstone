using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MyWpfApp.Model;

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

        /*Testing if split pdf sizes work from sizes of 1 pages up to 2000 page splits*/
        //test output list
        //test output file names
        //ensure when split that output files when summed have same number of pages as input files

    }
}
