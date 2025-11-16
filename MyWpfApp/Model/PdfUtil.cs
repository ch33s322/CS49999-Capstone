using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace MyWpfApp.Model
{
    public static class PdfUtil
    {
        /// <summary>
        /// Opens a PDF file using the system's default PDF viewer.
        /// Input can be either a full path or just a file name
        /// </summary>
        /// <param name = "pdfPath">
        /// Full path to PDF (e.g., C:\Users\Example\Documents\SampleJob001.pdf), or PDF file name (e.g., "SampleJob001.pdf)".
        /// When a file name is given, the function looks for the file in AppSettings.JobWell and AppSettings.JobDir.
        /// </param>
        public static void OpenPdfWithDefaultViewer(string pdfPath)
        {
            // Validates given path (not whitespace/null value)
            if(string.IsNullOrWhiteSpace(pdfPath))
            {
                MessageBox.Show("Invalid PDF file path.", "Error: View PDF", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string fullPath = pdfPath.Trim();

                // If only a file name is provided, try to resolve using 
                // JobWell and JobDir.
                if (!Path.IsPathRooted(pdfPath))
                {
                    // Check both JobWell and JobDir for PDF path
                    var fullPathJobWell = Path.Combine(AppSettings.JobWell, pdfPath);
                    var fullPathJobDir = Path.Combine(AppSettings.JobDir, pdfPath);

                    // If File exists at JobWell path(View original PDF), use that for fullPath
                    // If File exists at JobDir path (View split PDFs), use that for fullPath
                    // If File exists at neither, default fullPath to JobDir for Error
                    if (File.Exists(fullPathJobWell))
                    {
                        fullPath = fullPathJobWell;
                    }
                    else if (File.Exists(fullPathJobDir))
                    {
                        fullPath = fullPathJobDir;
                    }
                    else
                    {
                        fullPath = fullPathJobDir;
                    }

                }

                // Checking again for file existence
                if (!File.Exists(fullPath))
                {
                    MessageBox.Show($"PDF file not found: {fullPath}", "Error: View PDF", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Use default associated application for .pdf files
                var psi = new ProcessStartInfo
                {
                    FileName = fullPath,
                    UseShellExecute = true
                };

                Process.Start(psi);
            }
            catch (Win32Exception)
            {
                // This exception is thrown when no application is associated with .pdf files
                // Prompt user to set a default application
                var result = MessageBox.Show("Windows could not open this PDF because no default application " +
                    "is associated with .pdf files \n\n" +
                    "Would you like to choose a default application now?",
                    "No Default PDF Application",
                    MessageBoxButton.YesNo);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        Process.Start("ms-settings:defaultapps?filters=.pdf");
                    }
                    catch (Exception settingsEx)
                    {
                        MessageBox.Show($"Unable to open Windows settings:\n {settingsEx.Message}", "Error: View PDF", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                // Generic exception for unexpected issues
                MessageBox.Show($"An error occurred while trying to open the PDF file: {ex.Message}", "Error: View PDF", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

}
