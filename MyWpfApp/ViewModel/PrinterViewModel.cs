using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using MyWpfApp.Model;

namespace Printer.ViewModel
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

        // Load only printers persisted by PrintManager (AppSettings.PrinterStoreFile)
        private void LoadPrinters()
        {
            AvailablePrinters.Clear();
            DisposeWatchers();

            try
            {
                var storePath = AppSettings.PrinterStoreFile;
                if (!File.Exists(storePath))
                {
                    Debug.WriteLine("Persisted printer store not found; no printers to watch.");
                    return;
                }

                List<PersistedPrinterInfo> persisted;
                try
                {
                    using (var fs = File.OpenRead(storePath))
                    {
                        var ser = new DataContractJsonSerializer(typeof(List<PersistedPrinterInfo>));
                        persisted = ser.ReadObject(fs) as List<PersistedPrinterInfo> ?? new List<PersistedPrinterInfo>();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to read persisted printer store: {ex.Message}");
                    return;
                }

                foreach (var pinfo in persisted)
                {
                    var printerName = pinfo.Name ?? string.Empty;
                    if (string.IsNullOrEmpty(printerName))
                    {
                        // Skip empty-name entries (these are used for "unassigned" jobs)
                        continue;
                    }

                    // Query WMI for that specific printer
                    string escaped = printerName.Replace("\\", "\\\\");
                    string query = $"SELECT Name, PrinterStatus FROM Win32_Printer WHERE Name = '{escaped}'";
                    ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);

                    bool found = false;
                    foreach (ManagementObject printer in searcher.Get())
                    {
                        found = true;
                        string name = printer["Name"].ToString();
                        ushort wmiStatus = (ushort)printer["PrinterStatus"];

                        var newPrinter = new Model.Printer
                        {
                            Name = name,
                            Status = GetPrinterStatus(wmiStatus)
                        };

                        AvailablePrinters.Add(newPrinter);
                        WatchPrinterStatus(name);
                    }

                    if (!found)
                    {
                        // Persisted printer not present on system right now - still add so UI shows it
                        var newPrinter = new Model.Printer
                        {
                            Name = printerName,
                            Status = "Not Present"
                        };
                        AvailablePrinters.Add(newPrinter);

                        // Start watcher for when the device appears/changes
                        WatchPrinterStatus(printerName);
                    }
                }

                Debug.WriteLine($"Loaded {AvailablePrinters.Count} persisted printers and started watchers.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during LoadPrinters: {ex.Message}");
            }
        }

        // DTO matching the persisted structure in PrintManager (only fields needed here)
        [DataContract]
        private class PersistedPrinterInfo
        {
            [DataMember] public string Name { get; set; }
            [DataMember] public string Status { get; set; }
            [DataMember] public List<Job> Jobs { get; set; }
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
            Task.Run(() => {
                try { watcher.Start(); }
                catch (Exception ex) { Debug.WriteLine($"Watcher start failed for '{printerName}': {ex.Message}"); }
            });
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
            if (statusValue > 8) return "Error";

            Debug.WriteLine($"Status Code: {statusValue}");
            return $"Status Code: {statusValue}";
        }

        // ----------------------------------------------------------------------------------
        // RESOURCE CLEANUP (IDisposable IMPLEMENTATION)
        // ----------------------------------------------------------------------------------
        private void DisposeWatchers()
        {
            foreach (var watcher in _watchers)
            {
                try { watcher.Stop(); } catch { /* Ignore exception on stopping */ }
                try { watcher.Dispose(); } catch { /* ignore */ }
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
