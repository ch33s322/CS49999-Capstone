using FileSystemItemModel.Model;
using MyWpfApp.Model;
using static MyWpfApp.Model.PdfUtil;
using MyWpfApp.Utilities;
using Printer.ViewModel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Printing;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MyWpfApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly PrintManager _printManager;
        public Printer.Model.Printer _selectedPrinter;

        // Poller instance that watches InputDir
        private MyWpfApp.Model.PollAndArchive _poller;
        private string _currentPollerInputPath;
        private string _currentPollerArchivePath;
        private string _currentPollerJobPath;

        public MainWindow()
        {
            InitializeComponent();
            _printManager = new PrintManager();
            _printManager.JobsChanged += PrintManager_JobsChanged;

            // If an Adobe path was previously saved, ensure the PrintManager knows about it
            try
            {
                _printManager.AdobeReaderPath = AppSettings.AdobePath;
            }
            catch
            {
                // ignore
            }
           
            //Directory.Delete(AppSettings.JobDir, true);
            //Directory.Delete(AppSettings.PrinterDir, true);
            //File.WriteAllText(AppSettings., string.Empty);
            // Ensure the printer dropdown shows the initial placeholder when no selection exists.
            // (ComboBox controls are available after InitializeComponent.)
            try
            {
                PrinterSelect.SelectedIndex = -1;
                PrinterSelect.Text = "Select Printer";
            }
            catch
            {
                // ignore if control not present
            }

            // Populate the add printers combo with installed printers on the machine
            try
            {
                var installed = PrinterSettings.InstalledPrinters;
                var list = new List<string>();
                foreach (var name in installed)
                {
                    list.Add(name.ToString());
                }
                printerPickComboBox.ItemsSource = list;
                if (list.Any()) printerPickComboBox.SelectedIndex = 0;
                
            }
            catch (Exception ex)
            {
                // non-fatal; still allow manual input or other flows
                System.Diagnostics.Debug.WriteLine($"Failed to enumerate installed printers: {ex.Message}");
            }

            // Initialize MaxPages textbox with current setting
            try
            {
                MaxPagesTextBox.Text = AppSettings.MaxPages.ToString();
                MaxPagesValidationLabel.Text = string.Empty;
            }
            catch
            {
                // ignore if control not present or other issue
            }

            // Initialize InputDir textbox with current setting and persist on lost focus
            try
            {
                InputDir.Text = AppSettings.InputDir;
                InputDir.LostFocus += InputDir_LostFocus;
            }
            catch
            {
                // ignore if control not present
            }

            // Initialize ArchiveDir textbox with current setting and persist on lost focus
            try
            {
                archiveDirTextBox.Text = AppSettings.ArchiveDir;
                archiveDirTextBox.LostFocus += ArchiveDir_LostFocus;
            }
            catch
            {
                // ignore if control not present
            }

            // Initialize JobDir textbox with current setting and persist on lost focus
            try
            {
                jobDirTextBox.Text = AppSettings.JobDir;
                jobDirTextBox.LostFocus += JobDir_LostFocus;
            }
            catch
            {
                // ignore if control not present
            }

            // Initialize Adobe path textbox with current setting and persist on lost focus
            try
            {
                adobePathBox.Text = AppSettings.AdobePath;
                adobePathBox.LostFocus += AdobePathBox_LostFocus;
            }
            catch
            {
                // ignore if control not present
            }

            // Start poller if a valid InputDir is configured
            try
            {
                StartOrRestartPoller(AppSettings.InputDir, AppSettings.ArchiveDir, AppSettings.JobDir);
            }
            catch
            {
                // ignore startup errors
            }
        }
        private void PrintManager_JobsChanged(object sender, EventArgs e)
        {
            RefreshPrinterViewModels(); // reuse existing method to rebuild view-models
        }

        protected override void OnClosed(EventArgs e)
        {
            _printManager.JobsChanged -= PrintManager_JobsChanged;
            try { _poller?.Dispose(); } catch { }
            base.OnClosed(e);
        }

        private void SimplexDuplexCheckBoxChecked(object sender, RoutedEventArgs e)
        {
            //add code to change simplex duplex property
        }

        private void AddClick(object sender, RoutedEventArgs e)
        {

        }

        private void RightClickGetJob(object sender, RoutedEventArgs e)
        {
            // 1. Cast the sender to the clicked MenuItem
            MenuItem menuItem = sender as MenuItem;
            if (menuItem == null) return;

            // 2. The DataContext of the MenuItem is automatically the DataContext
            //    of the element the ContextMenu is attached to (the Job StackPanel).
            Job jobContext = menuItem.DataContext as Job;

            if (jobContext != null)
            {
                string header = menuItem.Header.ToString();

                // Example action based on the clicked menu item
                switch (header)
                {
                    case "View Job":
                        //MessageBox.Show($"Job Context: View requested for job '{jobContext.orgPdfName}'");
                        OpenPdfWithDefaultViewer(jobContext.orgPdfName);
                        break;
                    case "Remove Job":
                        //MessageBox.Show($"Job Context: Remove requested for job '{jobContext.orgPdfName}'");
                        break;
                }
            }
        }

        private void RightClickGetFile(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = sender as MenuItem;
            if (menuItem == null) return;

            string fileContext = menuItem.DataContext as string;
            if (fileContext != null)
            {
                DataGrid dataGrid = this.FindName("PrintJobManager") as DataGrid;

                if (dataGrid?.DataContext is PrinterViewModel viewModel)
                {
                    //get parent job for the clicked split file
                    Job parentJob = viewModel.GetJobByFileName(fileContext);

                    if (parentJob != null)
                    {
                        //get parent printer
                        Printer.Model.Printer parentPrinter = viewModel.GetPrinterByJob(parentJob);


                        string header = menuItem.Header.ToString();
                        // Example action based on the clicked menu item
                        switch (header)
                        {
                            case "Print Job":

                                // Log user-initiated print action (non-blocking)
                                try
                                {
                                    ActivityLogger.LogAction("PrintJob", $"User requested printing file '{fileContext}' to printer '{parentPrinter?.Name ?? "Unknown"}'");
                                }
                                catch(Exception ex)
                                {
                                    Debug.WriteLine($"Exception when logging print job: {ex}");
                                }

                                var server = new LocalPrintServer();
                                var printQueue = server.GetPrintQueue(parentPrinter.Name);
                                printQueue.Refresh();
                                var jobs = printQueue.GetPrintJobInfoCollection().Cast<PrintSystemJobInfo>().ToList();

                                //confirm job is not already in the print queue
                                var existingJob = jobs.FirstOrDefault(j => j.Name.Equals(parentJob.orgPdfName, StringComparison.OrdinalIgnoreCase));
                                if (existingJob != null)
                                {
                                    MessageBox.Show($"Job '{fileContext}' is already in the print queue for printer '{parentPrinter.Name}'.", "Print Job", MessageBoxButton.OK, MessageBoxImage.Information);
                                }
                                else
                                {
                                    // Code to send the job to the printer would go here
                                    MessageBox.Show($"Sending job '{fileContext}' to printer '{parentPrinter.Name}'.", "Print Job", MessageBoxButton.OK, MessageBoxImage.Information);

                                    //check printer status is a form of connected
                                    if (parentPrinter.Status == "Ready" || parentPrinter.Status == "Printing")
                                    {
                                        // Print the job
                                        var printResult = _printManager.PrintJob(fileContext, parentPrinter.Name);
                                        if (printResult)
                                        {
                                            MessageBox.Show($"Job '{fileContext}' sent to printer '{parentPrinter.Name}' successfully.", "Print Job", MessageBoxButton.OK, MessageBoxImage.Information);

                                            // Log successful send
                                            try
                                            {
                                                ActivityLogger.LogAction("PrintJobSuccess", $"File '{fileContext}' sent to printer '{parentPrinter.Name}'");
                                            }
                                            catch(Exception ex) { Debug.WriteLine($"Exception when validating print success: {ex}"); }
                                        }
                                        else
                                        {
                                            MessageBox.Show($"Failed to send job '{fileContext}' to printer '{parentPrinter.Name}'.", "Print Job", MessageBoxButton.OK, MessageBoxImage.Error);

                                            // Log failure
                                            try
                                            {
                                                ActivityLogger.LogAction("PrintJobFailure", $"Failed to send file '{fileContext}' to printer '{parentPrinter.Name}'");
                                            }
                                            catch(Exception ex) { Debug.WriteLine($"Exception when validating print failure: {ex}"); }
                                        }
                                    }
                                }

                                break;
                            case "View Job":
                                OpenPdfWithDefaultViewer(fileContext);
                                break;
                            case "Move Job":
                                {
                                    // gather available printers excluding current and excluding blank/unassigned
                                    var allPrinters = _printManager.getAllPrinters()
                                        .Where(p => !string.IsNullOrWhiteSpace(p))
                                        .Distinct(StringComparer.OrdinalIgnoreCase)
                                        .OrderBy(p => p)
                                        .ToList();

                                    // exclude the current printer of this job
                                    if (!string.IsNullOrWhiteSpace(parentJob.printerName))
                                    {
                                        allPrinters = allPrinters
                                            .Where(p => !string.Equals(p, parentJob.printerName, StringComparison.OrdinalIgnoreCase))
                                            .ToList();
                                    }

                                    if (allPrinters.Count == 0)
                                    {
                                        MessageBox.Show("There are no other printers to move this split PDF to.", "Move Job", MessageBoxButton.OK, MessageBoxImage.Information);
                                        break;
                                    }

                                    // show dialog to pick the destination printer
                                    var dlg = new MoveJobDialog(allPrinters, parentJob) { Owner = this };
                                    bool? result = dlg.ShowDialog();
                                    if (result == true && !string.IsNullOrWhiteSpace(dlg.SelectedPrinterName))
                                    {
                                        try
                                        {
                                            var moved = _printManager.MoveSplitFileToPrinter(parentJob.jobId, fileContext, dlg.SelectedPrinterName);
                                            if (moved)
                                            {
                                                RefreshPrinterViewModels();
                                                MessageBox.Show($"Split PDF '{fileContext}' moved to printer '{dlg.SelectedPrinterName}'.", "Move Job", MessageBoxButton.OK, MessageBoxImage.Information);
                                                try { ActivityLogger.LogAction("MoveSplitFile", $"User moved '{fileContext}' from job {parentJob.jobId} to '{dlg.SelectedPrinterName}'"); }
                                                catch(Exception ex) { Debug.WriteLine($"Exception when logging job move: {ex}"); }
                                            }
                                            else
                                            {
                                                MessageBox.Show("Move cancelled or file not found in the job.", "Move Job", MessageBoxButton.OK, MessageBoxImage.Information);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            MessageBox.Show($"Failed to move split PDF: {ex.Message}", "Move Job", MessageBoxButton.OK, MessageBoxImage.Error);
                                        }
                                    }
                                    break;
                                }
                            case "Remove Job":
                                // Confirm with the user
                                var confirm = MessageBox.Show(
                                    $"Are you sure you want to remove '{fileContext}' from job '{parentJob.orgPdfName}'?",
                                    "Confirm Remove",
                                    MessageBoxButton.YesNo,
                                    MessageBoxImage.Question);

                                if (confirm != MessageBoxResult.Yes)
                                    break;

                                try
                                {
                                    bool removed = _printManager.RemoveSplitFileFromJob(parentJob.jobId, fileContext);
                                    if (removed)
                                    {
                                        // Refresh view-models so UI reflects the removed split file
                                        RefreshPrinterViewModels();

                                        MessageBox.Show($"Removed file '{fileContext}' from job '{parentJob.orgPdfName}'.", "Remove Job", MessageBoxButton.OK, MessageBoxImage.Information);
                                        try { ActivityLogger.LogAction("RemoveSplitFile", $"User removed '{fileContext}' from job {parentJob.jobId}"); }
                                        catch(Exception ex) { Debug.WriteLine($"Exception when logging job removal: {ex}"); }
                                    }
                                    else
                                    {
                                        MessageBox.Show($"Failed to remove '{fileContext}'. It may not be part of the job or deletion failed.", "Remove Job", MessageBoxButton.OK, MessageBoxImage.Error);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show($"Error removing file '{fileContext}': {ex.Message}", "Remove Job", MessageBoxButton.OK, MessageBoxImage.Error);
                                }

                                break;
                        }
                    }
                }
            }
            else
            {
                MessageBox.Show("File context is null.");
            }
        }

        private void RightClickMoveJob(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                var job = menuItem.DataContext as Job;
                if (job != null)
                {
                    MoveJob(job);
                }
            }
        }
        private void MoveJob(Job job)
        {
            if (job == null) return;

            // Get all printers except current and empty/unassigned
            var all = _printManager.getAllPrinters()
                                   .Where(p => !string.IsNullOrWhiteSpace(p) &&
                                               !string.Equals(p, job.printerName, StringComparison.OrdinalIgnoreCase))
                                   .OrderBy(p => p)
                                   .ToList();

            if (all.Count == 0)
            {
                MessageBox.Show("There are no other printers to move this job to.", "Move Job", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new MoveJobDialog(all, job) { Owner = this };
            bool? result = dlg.ShowDialog();
            if (result == true && !string.IsNullOrWhiteSpace(dlg.SelectedPrinterName))
            {
                try
                {
                    _printManager.SetJobPrinter(job.jobId, dlg.SelectedPrinterName);
                    RefreshPrinterViewModels();
                    MessageBox.Show($"Job '{job.orgPdfName}' moved to printer '{dlg.SelectedPrinterName}'.", "Move Job", MessageBoxButton.OK, MessageBoxImage.Information);
                    try { ActivityLogger.LogAction("MoveJob", $"Moved job {job.jobId} to printer '{dlg.SelectedPrinterName}'"); } catch { }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to move job: {ex.Message}", "Move Job", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Queue a job: create via JobCreator.MakeJobAsync then hand to PrintManager.QueueJob
        private async void SendJobClick(object sender, RoutedEventArgs e)
        {
            var sendButton = sender as Button;

            // Determine selected printer from the PrinterSelect combobox
            _selectedPrinter = PrinterSelect.SelectedItem as Printer.Model.Printer;
            string printerName = _selectedPrinter?.Name;
            Debug.WriteLine(printerName);

            if (string.IsNullOrWhiteSpace(printerName))
            {
                MessageBox.Show("Select a target printer from the printer dropdown before sending a job.", "Send Job", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Determine selected file from the tree view
            var selectedItem = fileTreeView.SelectedItem as FileSystemItem;
            if (selectedItem == null)
            {
                MessageBox.Show("Select a file from the file tree to create a job from.", "Send Job", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // The JobCreator expects the pdf name to be present in AppSettings.JobWell.
            // Use the selected item's Name as the PDF file name.
            string pdfName = selectedItem.Name;
            if (string.IsNullOrWhiteSpace(pdfName))
            {
                MessageBox.Show("Selected file has no name.", "Send Job", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Determine simplex/duplex. Checked == duplex (label shows "Duplex"), so Simplex = not checked.
            bool simplex = !(SimplexDuplexCheckBox.IsChecked ?? false);

            // Temporarily disable the "Send Job" button to prevent multiple clicks
            if (sendButton != null)
            {
                sendButton.IsEnabled = false;
            }

            try
            {
                // Create job (this may do IO and CPU work; call async method)
                var pdfSplitter = new PdfSplitter();
                var creator = new JobCreator(pdfSplitter);
                var job = await creator.MakeJobAsync(printerName, pdfName, simplex).ConfigureAwait(false);
                Debug.WriteLine("Ouputting JOB...");
                Debug.WriteLine(job.ToString());
                // Queue the job on the UI thread
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _printManager.QueueJob(job);
                    Debug.WriteLine("Job queued.");
                    // Refresh view-models so UI reflects new job (the app currently creates separate VMs in XAML)
                    var vm = new Printer.ViewModel.PrinterViewModel();
                    //PrinterSelect.DataContext = vm; <-- obsolete, wipes the select printer dropdown on each 'send job' click
                    PrintJobManager.DataContext = vm;
                });

                if (File.Exists(AppSettings.JobWell  +  "\\" + pdfName))
                {
                    File.Delete(AppSettings.JobWell + "\\" + pdfName);                  
                }
                MessageBox.Show($"Job created and queued for printer '{printerName}'.", "Send Job", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.IO.FileNotFoundException fnf)
            {
                MessageBox.Show($"Source PDF not found in JobWell: {fnf.FileName}", "Send Job", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to create or queue job: {ex.Message}", "Send Job", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Re-enable the "Send Job" button
                if (sendButton != null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => sendButton.IsEnabled = true);
                }
            }
        }

        private void DirectorySelectTextChanged(object sender, TextChangedEventArgs e)
        {
            // existing wiring in XAML; no immediate persistence here to avoid saving on every keystroke.
            // Display or other real-time UI reaction could be added here if desired.
        }

        // Persist InputDir when user finishes editing (LostFocus). Validate and create directory.
        private void InputDir_LostFocus(object sender, RoutedEventArgs e)
        {
            var tb = sender as TextBox;
            if (tb == null) return;

            var newPath = tb.Text?.Trim();
            if (string.IsNullOrWhiteSpace(newPath))
            {
                MessageBox.Show("Input directory cannot be empty.", "Invalid input directory", MessageBoxButton.OK, MessageBoxImage.Warning);
                tb.Text = AppSettings.InputDir;
                return;
            }

            try
            {
                // Setting AppSettings.InputDir will normalize, create directory (try) and persist
                AppSettings.InputDir = newPath;

                // Ensure archive and jobwell directories exist before starting poller
                try { Directory.CreateDirectory(AppSettings.ArchiveDir); } catch { }
                try { Directory.CreateDirectory(AppSettings.JobWell); } catch { }

                StartOrRestartPoller(AppSettings.InputDir, AppSettings.ArchiveDir, AppSettings.JobDir);

                MessageBox.Show($"Input directory set to:\n{AppSettings.InputDir}", "Settings saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to set input directory: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                tb.Text = AppSettings.InputDir;
            }
        }

        // Persist ArchiveDir when user finishes editing (LostFocus). Validate and create directory.
        private void ArchiveDir_LostFocus(object sender, RoutedEventArgs e)
        {
            var tb = sender as TextBox;
            if (tb == null) return;

            var newPath = tb.Text?.Trim();
            if (string.IsNullOrWhiteSpace(newPath))
            {
                MessageBox.Show("Archive directory cannot be empty.", "Invalid archive directory", MessageBoxButton.OK, MessageBoxImage.Warning);
                tb.Text = AppSettings.ArchiveDir;
                return;
            }

            try
            {
                AppSettings.ArchiveDir = newPath;

                // Ensure directories exist before restarting poller
                try { Directory.CreateDirectory(AppSettings.InputDir); } catch { }
                try { Directory.CreateDirectory(AppSettings.JobWell); } catch { }

                StartOrRestartPoller(AppSettings.InputDir, AppSettings.ArchiveDir, AppSettings.JobDir);

                MessageBox.Show($"Archive directory set to:\n{AppSettings.ArchiveDir}", "Settings saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to set archive directory: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                tb.Text = AppSettings.ArchiveDir;
            }
        }

        // Persist JobDir when user finishes editing (LostFocus). Validate and create directory.
        private void JobDir_LostFocus(object sender, RoutedEventArgs e)
        {
            var tb = sender as TextBox;
            if (tb == null) return;

            var newPath = tb.Text?.Trim();
            if (string.IsNullOrWhiteSpace(newPath))
            {
                MessageBox.Show("Job directory cannot be empty.", "Invalid job directory", MessageBoxButton.OK, MessageBoxImage.Warning);
                tb.Text = AppSettings.JobDir;
                return;
            }

            try
            {
                AppSettings.JobDir = newPath;

                // Ensure required directories exist before restarting poller
                try { Directory.CreateDirectory(AppSettings.InputDir); } catch { }
                try { Directory.CreateDirectory(AppSettings.ArchiveDir); } catch { }

                StartOrRestartPoller(AppSettings.InputDir, AppSettings.ArchiveDir, AppSettings.JobDir);

                MessageBox.Show($"Job directory set to:\n{AppSettings.JobDir}", "Settings saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to set job directory: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                tb.Text = AppSettings.JobDir;
            }
        }

        // Persist AdobePath when user finishes editing (LostFocus). Normalize and persist.
        private void AdobePathBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var tb = sender as TextBox;
            if (tb == null) return;

            var newPath = tb.Text?.Trim() ?? string.Empty;
            try
            {
                // Setting AppSettings.AdobePath will normalize (if non-empty) and persist
                AppSettings.AdobePath = newPath;
                // Inform PrintManager about the change
                try { _printManager.AdobeReaderPath = AppSettings.AdobePath; } catch { }

                MessageBox.Show($"Adobe path set to:\n{AppSettings.AdobePath}", "Settings saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to set Adobe path: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                tb.Text = AppSettings.AdobePath;
            }
        }

        // Start a new PollAndArchive or restart if input or archive or job path changed.
        private void StartOrRestartPoller(string inputPath, string archivePath, string jobPath)
        {
            if (string.IsNullOrWhiteSpace(inputPath) || string.IsNullOrWhiteSpace(archivePath) || string.IsNullOrWhiteSpace(jobPath)) return;

            // If already running for same input, archive and job path, nothing to do
            if (_poller != null &&
                string.Equals(_currentPollerInputPath, inputPath, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(_currentPollerArchivePath, archivePath, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(_currentPollerJobPath, jobPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Dispose old poller if present
            try
            {
                if (_poller != null)
                {
                    _poller.Dispose();
                    _poller = null;
                    _currentPollerInputPath = null;
                    _currentPollerArchivePath = null;
                    _currentPollerJobPath = null;
                }
            }
            catch
            {
                // swallow disposal errors
            }

            // Ensure required directories exist
            try { Directory.CreateDirectory(inputPath); } catch { }
            try { Directory.CreateDirectory(archivePath); } catch { }
            try { Directory.CreateDirectory(jobPath); } catch { }
            try { Directory.CreateDirectory(AppSettings.JobWell); } catch { }

            try
            {
                // Create and start the new poller (poller uses JobWell as its output)
                _poller = new MyWpfApp.Model.PollAndArchive(inputPath, archivePath, AppSettings.JobWell);
                _poller.StartWatching();
                _currentPollerInputPath = inputPath;
                _currentPollerArchivePath = archivePath;
                _currentPollerJobPath = jobPath;
                Debug.WriteLine($"PollAndArchive started for input: {inputPath}, archive: {archivePath}, jobDir: {jobPath}");
            }
            catch (Exception ex)
            {
                // If construction fails, clear poller state and notify user
                try { _poller?.Dispose(); } catch { }
                _poller = null;
                _currentPollerInputPath = null;
                _currentPollerArchivePath = null;
                _currentPollerJobPath = null;
                MessageBox.Show($"Failed to start PollAndArchive for '{inputPath}' -> '{archivePath}': {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrinterSelectSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // If the selection becomes null (ie. printer removal), dropdown showsd placeholder text
            if (PrinterSelect.SelectedItem == null)
            {
                PrinterSelect.Text = "Select Printer";
            }
        }

        private void PrintJobManager_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
        {
            if (sender is TreeViewItem item && item.DataContext is FileSystemItem fsItem)
            {
                fsItem.LoadChildren();
            }
        }

        // Called when user clicks Add on the printer dropdown
        private void AddPrinterButton_Click(object sender, RoutedEventArgs e)
        {
            var name = printerPickComboBox.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Select a printer from the dropdown before clicking Add.", "Add Printer", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                _printManager.AddPrinter(name);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to add printer: {ex.Message}", "Add Printer", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Refresh PrinterViewModel instances in the UI so new printer appears
            RefreshPrinterViewModels();

            // Optionally remove the added item from the dropdown or keep it
            //printerPickComboBox.Items.Refresh();

            MessageBox.Show($"Printer '{name}' added.", "Add Printer", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void RemovePrinterButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedPrinter = currentPrinterPickComboBox.SelectedItem as Printer.Model.Printer;
            string printerName = selectedPrinter?.Name;
            if (string.IsNullOrWhiteSpace(printerName))
            {
                MessageBox.Show("Select a printer from the printer dropdown before clicking Remove.", "Remove Printer", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                _printManager.RemovePrinter(printerName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to remove printer: {ex.Message}", "Remove Printer", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Refresh PrinterViewModel instances used in UI so removed printer disappears, clear the selection
            RefreshPrinterViewModels();

            MessageBox.Show($"Printer '{printerName}' removed.", "Remove Printer", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Apply MaxPages setting
        private void ApplyMaxPagesButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(MaxPagesTextBox.Text))
            {
                MessageBox.Show("Please enter a value (number greater than 0, no commas).", "Invalid value", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(MaxPagesTextBox.Text.Trim(), out int newMax) || newMax < 1)
            {
                MessageBox.Show("Please enter an integer greater than 0, without commas.", "Invalid value", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            AppSettings.MaxPages = newMax;
            MessageBox.Show($"Max pages per split set to {newMax}.", "Settings updated", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Helper to replace the PrinterViewModel instances used by UI controls
        private void RefreshPrinterViewModels()
        {
            var vm = new Printer.ViewModel.PrinterViewModel();
            PrinterSelect.DataContext = vm;
            PrintJobManager.DataContext = vm;
            currentPrinterPickComboBox.DataContext = vm;

            // Clear any currently selected item and set text to placeholder
            // SelectedIndex = -1 guarantees no selection
            PrinterSelect.SelectedIndex = -1;
            PrinterSelect.Text = "Select Printer";
        }
    }
}