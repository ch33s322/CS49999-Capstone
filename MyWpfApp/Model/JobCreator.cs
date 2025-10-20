using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace MyWpfApp.Model
{
    public class JobCreator
    {
        //reference to pdf splitter
        private readonly PdfSplitter m_pdfSplitter;
        //constructor
        public JobCreator(PdfSplitter pdfSplitter)
        {
            m_pdfSplitter = pdfSplitter;
        }

        public async Task<Job> MakeJobAsync(string printerName, string pdfName, bool simplex)
        {
            var inputPdfPath = Path.Combine(AppSettings.JobWell, pdfName);
            if (!File.Exists(inputPdfPath))
            {
                throw new FileNotFoundException("Pdf not found in JobWell", inputPdfPath);
            }

            if (!Directory.Exists(AppSettings.JobDir))
            {
                Directory.CreateDirectory(AppSettings.JobDir);
            }

            var splitFiles = await Task.Run(() => m_pdfSplitter.SplitPdf(inputPdfPath, AppSettings.JobDir, AppSettings.MaxPages));

            var job = new Job(printerName, splitFiles, simplex, pdfName);
            return job;
        }

        public void DeletePdfFromJobWell(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return;

            //build full path in JobWell
            var fullPath = Path.Combine(AppSettings.JobWell, fileName);

            //ensure the file is inside the JobWell directory
            if (!fullPath.StartsWith(AppSettings.JobWell, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Cannot delete files outside the JobWell directory.");

            if (File.Exists(fullPath))
            {
                try
                {
                    File.Delete(fullPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to delete file '{fullPath}': {ex.Message}");
                }
            }
        }
    }
}
