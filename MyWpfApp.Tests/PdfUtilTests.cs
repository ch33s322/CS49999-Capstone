using MyWpfApp.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace MyWpfApp.Tests
{
    [Collection("PrinterTestsCollection")]
    public class PdfUtilTests
    {
        public PdfUtilTests()
        {
            // Make sure standard directories exist before running tests
            AppSettings.EnsureDirectoriesExist();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void OpenPdf_WithNullOrWhitespacePath_DoesNotThrow(string? input)
        {
            var ex = Record.Exception(() => 
                MyWpfApp.Model.PdfUtil.OpenPdfWithDefaultViewer(input)
            );

            Assert.Null(ex);
        }

        [Fact]
        public void OpenPdf_NonexistentRelativeFile_DoesNotThrow()
        {
            var dneFile = $"doesnotexist#:N.pdf";

            var ex = Record.Exception(() => MyWpfApp.Model.PdfUtil.OpenPdfWithDefaultViewer(dneFile));
            Assert.Null(ex);
        }

        [Fact]
        public void OpenPdf_FileInJobDir_DoesNotThrow()
        {
            var fileName = $"PDFUTIL_JOBDIR_{Guid.NewGuid():N}.pdf";
            var fullPath = Path.Combine(AppSettings.JobDir, fileName);

            Directory.CreateDirectory(AppSettings.JobDir);
            File.WriteAllText(fullPath, "Lorem ipsum dolor sit amet.");

            try
            {
                var ex = Record.Exception(() => MyWpfApp.Model.PdfUtil.OpenPdfWithDefaultViewer(fileName));

                Assert.Null(ex);
            }
            finally
            {
                try
                {
                    if (File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                    }
                }
                catch
                {

                }
            }
        }

        [Fact]
        public void OpenPdf_FileInJobWell_DoesNotThrow()
        {
            var fileName = $"PDFUTIL_JOBWELL_{Guid.NewGuid():N}.pdf";
            var fullPath = Path.Combine(AppSettings.JobWell, fileName);

            Directory.CreateDirectory(AppSettings.JobWell);
            File.WriteAllText(fullPath, "Lorem ipsum dolor sit amet.");

            try
            {
                var ex = Record.Exception(() => MyWpfApp.Model.PdfUtil.OpenPdfWithDefaultViewer(fileName));

                Assert.Null(ex);
            }
            finally
            {
                try
                {
                    if (File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                    }
                }
                catch
                {

                }
            }
        }

        [Fact]
        public void OpenPdf_AbsolutePath_DoesNotThrow()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "PdfUtilTests");
            Directory.CreateDirectory(tempRoot);

            var fullPath = Path.Combine(tempRoot, $"PDFUTIL_ABS_{Guid.NewGuid:N}.pdf");
            File.WriteAllText(fullPath, "Lorem ipsum dolor sit amet.");

            try
            {
                var ex = Record.Exception(() => MyWpfApp.Model.PdfUtil.OpenPdfWithDefaultViewer(fullPath));

                Assert.Null(ex);
            }
            finally
            {
                try
                {
                    if (File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                    }
                }
                catch
                {

                }
            }
        }

     
    }
}
