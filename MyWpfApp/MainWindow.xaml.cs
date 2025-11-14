using FileSystemItemModel.Model;
using MyWpfApp.Model;
using Printer.ViewModel;
using System;
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

        public MainWindow()
        {
            InitializeComponent();
            _printManager = new PrintManager();

            //Directory.Delete(AppSettings.JobDir, true);
            //Directory.Delete(AppSettings.PrinterDir, true);
            //File.WriteAllText(AppSettings., string.Empty);
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
                        MessageBox.Show($"Job Context: View requested for job '{jobContext.orgPdfName}'");
                        break;
                    case "Remove Job":
                        MessageBox.Show($"Job Context: Remove requested for job '{jobContext.orgPdfName}'");
                        break;
                }
            }
        }

        private void RightClickGetFile(object sender, RoutedEventArgs e)
        {
            // 1. Cast the sender to the clicked MenuItem
            MenuItem menuItem = sender as MenuItem;
            if (menuItem == null) return;
            // 2. The DataContext of the MenuItem is automatically the DataContext
            //    of the element the ContextMenu is attached to (the File StackPanel).
            string fileContext = menuItem.DataContext as string;
            if (fileContext != null)
            {
                DataGrid dataGrid = this.FindName("PrintJobManager") as DataGrid;

                if (dataGrid?.DataContext is PrinterViewModel viewModel)
                {
                    Job parentJob = viewModel.GetJobByFileName(fileContext);

                    if (parentJob != null)
                    {
                        Printer.Model.Printer parentPrinter = viewModel.GetPrinterByJob(parentJob);


                        string header = menuItem.Header.ToString();
                        // Example action based on the clicked menu item
                        switch (header)
                        {
                            case "Print Job":
                                MessageBox.Show($"File Context: Print requested for file '{fileContext}' " +
                                    $"with Parent Job '{parentJob.orgPdfName}'" +
                                    $"in Printer '{parentPrinter.Name}'");
                                break;
                            case "View Job":
                                MessageBox.Show($"File Context: View requested for file '{fileContext}'" +
                                    $"with Parent Job '{parentJob.orgPdfName}'" +
                                    $"in Printer '{parentPrinter.Name}'");
                                break;
                            case "Move Job":
                                MessageBox.Show($"File Context: Move requested for file '{fileContext}'" +
                                    $"with Parent Job '{parentJob.orgPdfName}'" +
                                    $"in Printer '{parentPrinter.Name}'");
                                break;
                            case "Remove Job":
                                MessageBox.Show($"File Context: Remove requested for file '{fileContext}'" +
                                    $"with Parent Job '{parentJob.orgPdfName}'" +
                                    $"in Printer '{parentPrinter.Name}'");
                                break;
                        }
                    }
                    else
                    {
                        MessageBox.Show($"Parent job not found for file '{fileContext}'.");
                    }
                }
            }
            else
            {
                MessageBox.Show("File context is null.");
            }
        }

        private void RightClickPrintJob(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem )
            {
                // Do something with 'job'
                MessageBox.Show($"Printing job");
            }
        }

        private void RightClickRemoveJob(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                // Do something with 'job'
                MessageBox.Show($"Removing job");
            }
        }

        private void RightClickViewJob(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                // Do something with 'job'
                MessageBox.Show($"Viewing job");
            }
        }

        private void RightClickMoveJob(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                // Do something with 'job'
                MessageBox.Show($"Moving job");
            }
        }

        // Queue a job: create via JobCreator.MakeJobAsync then hand to PrintManager.QueueJob
        private async void SendJobClick(object sender, RoutedEventArgs e)
        {
            var sendButton = sender as Button;

            // Determine selected printer from the PrinterSelect combobox
            var selectedPrinter = PrinterSelect.SelectedItem as Printer.Model.Printer;
            string printerName = selectedPrinter?.Name;
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
                    PrinterSelect.DataContext = vm;
                    PrintJobManager.DataContext = vm;
                });

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
            //change current viewed directory
        }

        private void PrinterSelectSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //add code to handle printer selection changes
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

            // Refresh the PrinterViewModel instances used in the UI so the new printer appears.
            var vm = new Printer.ViewModel.PrinterViewModel();
            PrinterSelect.DataContext = vm;
            PrintJobManager.DataContext = vm;
            currentPrinterPickComboBox.DataContext = vm;

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
            // Refresh the PrinterViewModel instances used in the UI so the removed printer disappears.
            var vm = new Printer.ViewModel.PrinterViewModel();
            PrinterSelect.DataContext = vm;
            PrintJobManager.DataContext = vm;
            currentPrinterPickComboBox.DataContext = vm;
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
    }
}