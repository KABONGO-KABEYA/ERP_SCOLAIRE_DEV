using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public partial class FileStorageConfigWindow : Window
{
    public FileStorageConfigWindow(FileStorageConfigViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.RequestClose += success =>
        {
            DialogResult = success;
            Close();
        };

        viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        Loaded += (_, _) => RacineTextBox.Focus();
    }

    private void SyncRacineFromTextBox()
    {
        if (DataContext is FileStorageConfigViewModel viewModel)
        {
            viewModel.ApplyRacine(RacineTextBox.Text);
        }
    }

    private void TestAccessButton_OnClick(object sender, RoutedEventArgs e)
    {
        SyncRacineFromTextBox();
        if (DataContext is FileStorageConfigViewModel viewModel)
        {
            viewModel.TestConnectionCommand.Execute(null);
        }
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        SyncRacineFromTextBox();
        if (DataContext is FileStorageConfigViewModel viewModel)
        {
            viewModel.SaveCommand.Execute(null);
        }
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FileStorageConfigViewModel.IsConnectionSuccessful)
            || DataContext is not FileStorageConfigViewModel viewModel)
        {
            return;
        }

        if (viewModel.IsConnectionSuccessful is true)
        {
            StatusIcon.Kind = PackIconKind.CheckCircle;
            StatusIcon.Foreground = (Brush)FindResource("ErpSuccessBrush");
        }
        else if (viewModel.IsConnectionSuccessful is false)
        {
            StatusIcon.Kind = PackIconKind.AlertCircle;
            StatusIcon.Foreground = (Brush)FindResource("ErpDangerBrush");
        }
        else
        {
            StatusIcon.Kind = PackIconKind.InformationOutline;
            StatusIcon.Foreground = (Brush)FindResource("ErpTextSecondaryBrush");
        }
    }
}
