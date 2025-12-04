using PdfSharpCore.Pdf.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyWpfApp.Model
{
    public class PdfValidationResult
    {
        public bool Success { get; private set; } = true;
        public string ErrorMessage { get; private set; } = "";
        public List<string> PageErrors { get; } = new List<string>();

        public void Fail(string message)
        {
            Success = false;
            ErrorMessage = message;
        }

        public void AddPageError(int page, string message)
        {
            Success = false;
            PageErrors.Add($"Page {page}: {message}");
        }
    }

    public static class PdfValidator
    {
        public static PdfValidationResult ValidateSplitIntegrity(
        string originalPdfPath,
        IEnumerable<string> splitFiles,
        string splitDirectory)
        {
            var result = new PdfValidationResult();

            if (!File.Exists(originalPdfPath))
            {
                result.Fail("Original PDF file does not exist.");
                return result;
            }

            using (var original = PdfReader.Open(originalPdfPath, PdfDocumentOpenMode.Import))
            {
                int originalPages = original.PageCount;
                int reconstructedPages = 0;

                var originalHashes = new List<string>();
                for (int i = 0; i < originalPages; i++)
                    originalHashes.Add(PdfUtil.ComputePageHash(original, i));

                var reconstructedHashes = new List<string>();

                foreach (var name in splitFiles)
                {
                    var path = Path.Combine(splitDirectory, name);

                    if (!File.Exists(path))
                    {
                        result.Fail($"Missing split PDF: {path}");
                        return result;
                    }

                    using (var doc = PdfReader.Open(path, PdfDocumentOpenMode.Import))
                    {
                        reconstructedPages += doc.PageCount;

                        for (int p = 0; p < doc.PageCount; p++)
                            reconstructedHashes.Add(PdfUtil.ComputePageHash(doc, p));
                    }
                }

                // Validate page count
                if (reconstructedPages != originalPages)
                {
                    result.Fail(
                        $"Page count mismatch: Original={originalPages}, Split={reconstructedPages}");
                    return result;
                }

                // Validate per-page hashes
                for (int i = 0; i < originalPages; i++)
                {
                    if (originalHashes[i] != reconstructedHashes[i])
                        result.AddPageError(i + 1, "Content hash mismatch.");
                }

                return result;
            }
        }
    }
}
