using System;
using System.IO;
using System.Collections.Generic;
using System.Windows.Documents;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace MyWpfApp.Model
{
    public class PdfSplitter
    {
        //splits a pdf into smaller pdfs but slicing every maxPages amount of pages and returns of a list of all file names to the pdfs it created
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
            //output file name list
            List<string> outputNameList = new List<string>();
            //using statement ensures proper clean up of pdfdocument object because it uses idisposable interface
            using (PdfDocument input = PdfReader.Open(inputFilePath, PdfDocumentOpenMode.Import))
            {
                //open pdf and read from original file
                int inputPageAmt = input.PageCount;
                for (int i = 0; i < inputPageAmt; i += maxPages)
                {
                    //create new pdf
                    //determine where the end page should be
                    int endPage = Math.Min(i + maxPages, inputPageAmt);
                    using (PdfDocument output = new PdfDocument())
                    {
                        //copy pages from input pdf to output
                        for (int j = i; j < endPage; j++)
                        {
                            output.AddPage(input.Pages[j]);
                        }
                        //get original filename
                        string fileName = Path.GetFileNameWithoutExtension(inputFilePath) + $"_part_{i+1}-{endPage}.pdf";
                        //add file name to the
                        outputNameList.Add(fileName);
                        //create output path for file
                        string outputPath = Path.Combine(outputDirectory, fileName);
                        //save output pdf
                        output.Save(outputPath);
                    }
                }
            }
            return outputNameList;
        }
    }
}
