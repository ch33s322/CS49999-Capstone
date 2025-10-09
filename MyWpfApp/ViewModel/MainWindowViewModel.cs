using MyWpfApp.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Printing;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace MyWpfApp.ViewModel
{
    
    public class PrinterViewModel : INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private List<ManagementEventWatcher> _watchers = new List<ManagementEventWatcher>();

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
            AvailablePrinters = new ObservableCollection<Model.Printer>();
            LoadPrinters();
        }

        private void LoadPrinters()
        {
            // The list of printers will be stored here
            AvailablePrinters.Clear();
            DisposeWatchers();

            try
            {
                // Use LocalPrintServer to get a collection of print queues (printers)
                string query = "SELECT Name, PrinterStatus FROM Win32_Printer";
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);

                foreach (ManagementObject printer in searcher.Get())
                {
                    string name = printer["Name"].ToString();
                    ushort wmiStatus = (ushort)printer["PrinterStatus"];

                    var newPrinter = new Model.Printer
                    {
                        Name = name,
                        Status = GetPrinterStatus(wmiStatus)
                    };

                    AvailablePrinters.Add(newPrinter);

                    // Start the WMI event listener for this specific printer
                    WatchPrinterStatus(name);
                }

                Debug.WriteLine($"Loaded {AvailablePrinters.Count} printers and started watchers.");
            }
            catch // Handle exceptions (e.g., insufficient permissions)
            {
                // error message
                Debug.WriteLine($"Error during LoadPrinters: NOT GOOD");
            }
        }

        public void WatchPrinterStatus(string printerName)
        {
            // WQL (WMI Query Language) to select events
            // This query asks for notification every time the Win32_Printer instance for the specific printer is modified.
            string query = $@"SELECT * FROM __InstanceModificationEvent WITHIN 5 
                      WHERE TargetInstance ISA 'Win32_Printer' 
                      AND TargetInstance.Name = '{printerName.Replace("\\", "\\\\")}'";

            ManagementEventWatcher watcher = new ManagementEventWatcher(query);

            // Add watcher to the list
            _watchers.Add(watcher);

            // Set up the event handler
            watcher.EventArrived += (sender, e) =>
            {
                // WMI events often run on non-UI threads, so use Dispatcher to update the UI
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    // The event contains the NEW printer instance data
                    ManagementBaseObject newPrinter = (ManagementBaseObject)e.NewEvent["TargetInstance"];
                    string name = newPrinter["Name"].ToString();
                    ushort wmiStatus = (ushort)newPrinter["PrinterStatus"];

                    //use wmiStatus and output a string for the status
                    string newStatus = GetPrinterStatus(wmiStatus);

                    UpdatePrinterInCollection(name, newStatus);
                });
            };

            // Start listening
            Task.Run(() => watcher.Start());
        }

        private void UpdatePrinterInCollection(string name, string newStatus)
        {
            var printerToUpdate = AvailablePrinters.FirstOrDefault(p => p.Name == name);

            if (printerToUpdate != null)
            {
                // This setter must trigger INotifyPropertyChanged for the UI to update
                printerToUpdate.Status = newStatus;
            }
        }

        private string GetPrinterStatus(ushort statusValue)
        {
            Debug.WriteLine($"Status UPDATE!!!!!!!!!");
            Debug.WriteLine($"Status Code: {statusValue}");
                    // Check for the most important "down" states
                    if (statusValue == 1) return "Not Connected";
                    if (statusValue == 3) return "Ready";
                    if (statusValue == 4) return "Printing";
                    if (statusValue == 7 || statusValue == 2) return "Offline/Unavailable";
                    //if (statusValue == 7) return "Toner/Ink Low";
                    if (statusValue > 8) return "Error";

                    // 2 is typically the ready state
                    //if (statusValue == 2) return "Ready";

                    // If a specific code isn't handled, return the raw code
                    Debug.WriteLine($"Status Code: {statusValue}");
                    return $"Status Code: {statusValue}";
        }

        // ----------------------------------------------------------------------------------
        // RESOURCE CLEANUP (IDisposable Implementation)
        // ----------------------------------------------------------------------------------
        private void DisposeWatchers()
        {
            foreach (var watcher in _watchers)
            {
                try { watcher.Stop(); } catch { /* Ignore exception on stopping */ }
                watcher.Dispose(); // Release unmanaged resources
            }
            _watchers.Clear();
        }

        public void Dispose()
        {
            DisposeWatchers();
            // Suppress finalization, as the object is already cleaned up
            GC.SuppressFinalize(this);
        }

        ~PrinterViewModel()
        {
            // Finalizer (called by GC if Dispose is not called explicitly)
            DisposeWatchers();
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
