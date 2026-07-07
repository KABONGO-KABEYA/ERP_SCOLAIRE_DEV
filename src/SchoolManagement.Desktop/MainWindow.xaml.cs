using System.Windows;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
