using System.Collections.Generic;
using System.Linq;
using System.Windows;
using MyWpfApp.Model;

namespace MyWpfApp
{
    public partial class MoveJobDialog : Window
    {
        public IList<string> Printers { get; }
        public string SelectedPrinterName { get; private set; }
        public string JobTitle { get; }

        public MoveJobDialog(IList<string> printers, Job job)
        {
            InitializeComponent();
            Printers = printers ?? new List<string>();
            JobTitle = $"Job: {job?.orgPdfName}";
            DataContext = this;

            // Preselect first if available
            if (Printers.Any())
            {
                PrinterCombo.SelectedIndex = 0;
            }
        }

        private void OkClick(object sender, RoutedEventArgs e)
        {
            var sel = PrinterCombo.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(sel))
            {
                MessageBox.Show("Please select a printer.", "Move Job", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            SelectedPrinterName = sel;
            DialogResult = true;
        }
    }
}