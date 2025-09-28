using System;
using System.IO;

namespace MyWpfApp
{
    public class PDFDocument
    {
        public string Path { get; set; }
        public PDFDocument(string path)
        {
            Path = path;
        }
    }

    public static class PDFUtils
    {
        // Copy PDF to archive directory
        public static void CopyPDFToArchive(PDFDocument pdf)
        {
            if (pdf == null)
                throw new ArgumentException("PDF object not found.");
            if (string.IsNullOrWhiteSpace(pdf.Path))
                throw new ArgumentException("Invalid PDF path.");

            // Hard-coded for testing, we'll need to change this later
            string archiveDir = @"C:\PDFArchive";
            Directory.CreateDirectory(archiveDir); // Just for testing

            string fileName = System.IO.Path.GetFileName(pdf.Path);
            string destPath = System.IO.Path.Combine(archiveDir, fileName);

            File.Copy(pdf.Path, destPath, overwrite: true); // Copy to archive
        }
    }
}