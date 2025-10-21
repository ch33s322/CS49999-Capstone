using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyWpfApp.Model
{
    public class Job
    {
        //name of the main PDF the job is made from
        public string orgPdfName { get; set; }
        //printer job is assigned to
        public string printerName { get; set; }
        //List of file names to print
        public List<string> fileNames { get; set; } = new List<string>();
        //unique id for the job
        public Guid jobId { get; set; } = Guid.NewGuid();
        //time stamp when job was created
        public DateTime dateTime { get; set; } = DateTime.Now;
        //simplex or duplex(by default it is simplex, false means duplex)
        public bool Simplex { get; set; } = true;

        // parameterless ctor — required for serializers and deserializers
        public Job()
        {
        }

        //constructor
        public Job(string PrinterName, List<string> FileNames, bool simplex, string OrgPdfName)
        {
            printerName = PrinterName;
            fileNames = new List<string>(FileNames);
            Simplex = simplex;
            orgPdfName = OrgPdfName;
        }

        public override string ToString()
        {
            return $"Job ID: {jobId}, Printer: {printerName}, Files: {string.Join(", ", fileNames)}, Simplex: {Simplex}, Original PDF: {orgPdfName}, Created At: {dateTime}";
        }

    }
}
