using FileSystemItemModel.Model;
using MyWpfApp.Model;
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
        /*Objects we use*/
        private JobCreator _jobCreator;
        private PdfSplitter _pdfSplitter;
        private PollAndArchive _pollAndArchive;
        private readonly PrintManager _printManager;

        public MainWindow()
        {    
            _pdfSplitter = new PdfSplitter();
            _jobCreator = new JobCreator(_pdfSplitter);
            //_pollAndArchive = new PollAndArchive()
            _printManager = new PrintManager();
            InitializeComponent();
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
        }

        private void SimplexDuplexCheckBoxChecked(object sender, RoutedEventArgs e)
        {
            //add code to change simplex duplex property
        }

        private void AddClick(object sender, RoutedEventArgs e)
        {

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

            try
            {
                // Create job (this may do IO and CPU work; call async method)
                var job = await _jobCreator.MakeJobAsync(printerName, pdfName, simplex).ConfigureAwait(false);
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
    }
}