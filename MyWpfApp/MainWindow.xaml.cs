using FileSystemItemModel.Model;
using MyWpfApp.Model;
using System;
using System.Collections.Generic;
using System.Drawing.Printing;
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

        // Populate the combo with installed printers on the machine
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

        private void SendJobClick(object sender, RoutedEventArgs e)
        {
            //add code for sending the jobs
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

            // Optionally remove the added item from the dropdown or keep it
            // printerPickComboBox.Items.Refresh();

            MessageBox.Show($"Printer '{name}' added.", "Add Printer", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
    }
}