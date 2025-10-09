using System;
using System.IO;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace MyWpfApp.Model
{
    internal class PdfSplitter
    {
        //splits a pdf into smaller pdfs but slicing every maxPages amount of pages
        public void SplitPdf(string inputFilePath, string outputDirectory, string archiveDirectory, int maxPages)
        {
            //check if inputpath exists
            if (!File.Exists(inputFilePath))
            {
                //throw error
                throw new FileNotFoundException("Input PDF not found.", inputFilePath);
            }
            //check if output directory exists
            if (!Directory.Exists(outputDirectory))
            {
                throw new DirectoryNotFoundException($"Output directory not found: {outputDirectory}");
            }
            //check if archive exists
            if (!Directory.Exists(archiveDirectory))
            {
                throw new DirectoryNotFoundException($"Archive directory not found: {archiveDirectory}");
            }
            //check if valid maxPagesAmount
            if (maxPages < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPages), "Max pages must be greater than 0");
            }

            //open pdf and read from original file
            PdfDocument input = PdfReader.Open(inputFilePath);
            int inputPageAmt = input.PageCount;

            for (int i = 0; i < inputPageAmt; i += maxPages)
            {
                //create new pdf
                PdfDocument output = new PdfDocument();
                //determine where the end page should be
                int endPage = Math.Min(i + maxPages, inputPageAmt);
                //copy pages from input pdf to output
                for (int j = 0; j < endPage; j++)
                {
                    output.AddPage(input.Pages[j]);
                }
                //get original filename
                string inputFileName = Path.GetFileNameWithoutExtension(inputFilePath);
                //create name for output file
                string outputFileName = Path.Combine(outputDirectory, $"{inputFileName}_part_{i / maxPages + 1}.pdf");
                //save output pdf
                output.Save(outputFileName);
                //dispose of output pdf
                output.Dispose();
            }
            //move input file to archive
            File.Move(inputFilePath, archiveDirectory);
            //TODO: Add error handling if same file name exists in archive already
            return;
        }
    }
}
