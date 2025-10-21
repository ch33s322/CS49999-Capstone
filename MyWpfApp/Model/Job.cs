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
        public string OrgPdfName { get; set; }
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
        public Job(string printerName, List<string> fileNames, bool simplex, string orgPdfName)
        {
            printerName = printerName ?? throw new ArgumentNullException(nameof(printerName));
            fileNames.AddRange(fileNames ?? throw new ArgumentNullException(nameof(fileNames)));
            Simplex = simplex;
            OrgPdfName = orgPdfName;
        }

    }
}
