using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyWpfApp.Model
{
    public class PrintManager
    {
        //associating printers to jobs
        //associating jobs to printers
        //set of all jobs
        //set of all printers

        //queues a job
        public void QueueJob(Job job)
        {

        }

        //release job
        public void ReleaseJob(Guid jobId)
        {

        }

        //set job to simplex/duplex
        public void SetJobSimplexFlag(Guid jobId, bool flag)
        {

        }

        //set job printer
        public void SetJobPrinter(Guid jobId, string printerName)
        {

        }

        //adds printer to printer set
        public void AddPrinter()
        {

        }
        //removes pritner from printer set and displaces all jobs to a special flag printer such that they are unassociated with a printer
        public void RemovePrinter()
        {

        }

        //used to get all jobs
        public List<Job> GetJobs()
        {
            //will implement
            return new List<Job>();
        }

        //get jobs for a printer, returns unsorted list of jobs for printer
        public List<Job> GetJobsForPrinter(string printer)
        {
            //will implement
            return new List<Job>();
        }

        //get all printers
        public List<string> getAllPrinters()
        {
            //will implement
            return new List<string>();
        }
    }
}
