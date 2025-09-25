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
    public MainWindow()
    {
        InitializeComponent();
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
    }
}