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
        [Fact]
        public void TestBadInputPath()
        {
            //create bad path that doesn't exist
            string badPath = "./asdfa.pdf";
            //create pdf splitter object
            var pdfSplitter = new PdfSplitter();
        }
    }
}
