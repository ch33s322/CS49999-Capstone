using MyWpfApp.Utilities;
using PdfSharpCore.Pdf.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Interop;

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
        /// <summary>
        /// Validates that the set of split PDFs exactly matches the content of the original PDF.
        /// Validation uses:
        ///   (1) Total page count comparison
        ///   (2) Per-page SHA-256 hashing of the original and reconstructed pages
        ///   (3) Missing split file detection
        /// 
        /// Pages must appear in identical order and contain identical content streams.
        /// 
        /// Note: This method assumes that splitting preserves page order.
        /// </summary>
        /// <param name="originalPdfPath">
        /// Full filesystem path to the original PDF.
        /// </param>
        /// <param name="splitFiles">
        /// Filenames (not full paths) returned by the splitter.
        /// They will be resolved relative to <paramref name="splitDirectory"/>.
        /// </param>
        /// <param name="splitDirectory">
        /// Directory containing the output split PDFs.
        /// </param>
        /// <returns>
        /// A <see cref="PdfValidationResult"/> describing success/failure and any per-page errors.
        /// </returns>
        public static PdfValidationResult ValidateSplitIntegrity(
        string originalPdfPath,
        IEnumerable<string> splitFiles,
        string splitDirectory)
        {
            var result = new PdfValidationResult();

            try
            {
                ActivityLogger.LogAction(
                    "PdfValidationStart",
                    $"Validating split integrity for '{Path.GetFileName(originalPdfPath)}' " +
                    $"with {((splitFiles is ICollection<string> c) ? c.Count : 0)} split files.");
            }
            catch { } // Logging failure, continue

            // Ensure original PDF exists
            if (!File.Exists(originalPdfPath))
            {
                string msg = $"Original PDF not found: {originalPdfPath}";
                result.Fail(msg);

                ActivityLogger.LogAction("PdfValidationError", msg);
                return result;
            }

            // Open original PDF for reading
            using (var original = PdfReader.Open(originalPdfPath, PdfDocumentOpenMode.Import))
            {
                int originalPages = original.PageCount;
                int reconstructedPages = 0;

                try
                {
                    // Log original page count
                    ActivityLogger.LogAction(
                        "PdfValidationInfo",
                        $"Original PDF page count: {originalPages}");
                }
                catch { } // Logging failure, continue

                // Compute per-page hashes for original PDF
                var originalHashes = new List<string>();
                for (int i = 0; i < originalPages; i++)
                    originalHashes.Add(PdfUtil.ComputePageHash(original, i));

                var reconstructedHashes = new List<string>();

                //Read each split PDF and build its hash list
                foreach (var name in splitFiles)
                {
                    var path = Path.Combine(splitDirectory, name);

                    if (!File.Exists(path))
                    {
                        string msg = $"Missing split PDF: {path}";

                        result.Fail(msg);
                        ActivityLogger.LogAction("PdfValidationError", msg);
                        return result;
                    }

                    using (var doc = PdfReader.Open(path, PdfDocumentOpenMode.Import))
                    {
                        reconstructedPages += doc.PageCount;

                        for (int p = 0; p < doc.PageCount; p++)
                            reconstructedHashes.Add(PdfUtil.ComputePageHash(doc, p));
                    }

                    try
                    {
                        // Log processing of split file
                        ActivityLogger.LogAction(
                            "PdfValidationInfo",
                            $"Processed split file '{name}'.");
                    }
                    catch { } // Logging failure, continue
                }

                // Validate page count
                if (reconstructedPages != originalPages)
                {
                    string msg = $"Reconstructed PDF page count ({reconstructedPages}) " +
                                 $"does not match original ({originalPages}).";
                    result.Fail(msg);
                    ActivityLogger.LogAction("PdfValidationError", msg);

                    return result;
                }

                // Validate per-page hashes
                for (int i = 0; i < originalPages; i++)
                {
                    if (originalHashes[i] != reconstructedHashes[i])
                    {
                        string msg = $"Page {i + 1}: Content hash mismatch.";
                        result.AddPageError(i + 1, "Content hash mismatch.");

                        ActivityLogger.LogAction("PdfValidationPageMismatch", msg);
                    }
                }

                // Log final validation result
                if (!result.Success)
                {
                    ActivityLogger.LogAction(
                        "PdfValidationFailed",
                        $"Validation failed for '{Path.GetFileName(originalPdfPath)}'. " +
                        $"{result.PageErrors.Count} mismatched pages.");
                    return result;
                }

                // Log validation success
                try
                {
                    ActivityLogger.LogAction(
                        "PdfValidationComplete",
                        $"Validation SUCCESS for '{Path.GetFileName(originalPdfPath)}'. " +
                        $"Total pages={originalPages}, Split files processed={((splitFiles is ICollection<string> sc) ? sc.Count : 0)}.");
                }
                catch { }

                // Return final result
                // If any page errors were added, Success will be false
                return result;
            }
        }
    }
}
