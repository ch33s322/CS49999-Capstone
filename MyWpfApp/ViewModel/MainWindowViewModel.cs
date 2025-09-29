using MyWpfApp.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Printing;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace MyWpfApp.ViewModel
{
    
    public class PrinterViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private ObservableCollection<Model.Printer> _availablePrinters;
        public ObservableCollection<Model.Printer> AvailablePrinters
        {
            get { return _availablePrinters; }
            set
            {
                _availablePrinters = value;
                OnPropertyChanged(nameof(AvailablePrinters));
            }
        }

        public PrinterViewModel()
        {
            LoadPrinters();
        }

        private void LoadPrinters()
        {
            // The list of printers will be stored here
            var printersList = new ObservableCollection<Model.Printer>();

            try
            {
                // Use LocalPrintServer to get a collection of print queues (printers)
                var printServer = new LocalPrintServer();
                PrintQueueCollection printQueues = printServer.GetPrintQueues(
                    new EnumeratedPrintQueueTypes[] {
                    EnumeratedPrintQueueTypes.Local,
                    EnumeratedPrintQueueTypes.Connections
                    });

                // Get the default printer name for comparison
                string defaultPrinterName = printServer.DefaultPrintQueue?.Name;

                foreach (PrintQueue queue in printQueues)
                {
                    printersList.Add(new Model.Printer
                    {
                        Name = queue.Name
                    });
                }

                Debug.WriteLine("--- Available Printers (Debug Log) ---");
                if (printersList.Count == 0)
                {
                    Debug.WriteLine("No printers found.");
                }
                else
                {
                    foreach (var printer in printersList)
                    {
                        //string defaultIndicator = printer.IsDefault ? "(DEFAULT)" : "";

                        // Use Debug.WriteLine instead of Console.WriteLine
                        Debug.WriteLine($"Name: {printer.Name,-40}");
                    }
                }
                Debug.WriteLine("--------------------------------------");
            }
            catch // Handle exceptions (e.g., insufficient permissions)
            {
                // Fallback to simpler method if System.Printing fails or for non-WPF dependencies
                foreach (string printerName in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
                {
                    printersList.Add(new Model.Printer { Name = printerName });
                }
            }

        AvailablePrinters = printersList;
        }
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
