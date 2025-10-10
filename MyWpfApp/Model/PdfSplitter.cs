using System;
using System.IO;
using System.Collections.Generic;
using System.Windows.Documents;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace MyWpfApp.Model
{
    internal class PdfSplitter
    {
        //splits a pdf into smaller pdfs but slicing every maxPages amount of pages
        public List<string> SplitPdf(string inputFilePath, string outputDirectory, int maxPages)
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
            //check if valid maxPagesAmount
            if (maxPages < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPages), "Max pages must be greater than 0");
            }

            //open pdf and read from original file
            PdfDocument input = PdfReader.Open(inputFilePath);
            int inputPageAmt = input.PageCount;

            //output file name list
            List<string> outputNameList = new List<string>();

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
                string fileName = Path.GetFileNameWithoutExtension(inputFilePath)+$"_part_{i}->{endPage}.pdf";
                //add file name to the
                outputNameList.Add(fileName);
                //create output path for file
                string outputPath = Path.Combine(outputDirectory, fileName);
                //save output pdf
                output.Save(outputPath);
                //dispose of output pdf
                output.Dispose();
            }
            //TODO: Add error handling if same file name exists in archive already
            return outputNameList;
        }
    }
}
