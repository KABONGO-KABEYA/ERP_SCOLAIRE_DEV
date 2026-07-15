using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public partial class DatabaseServerConfigWindow : Window
{
    public DatabaseServerConfigWindow(DatabaseServerConfigViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.RequestClose += success =>
        {
            DialogResult = success;
            Close();
        };

        viewModel.PasswordResetRequested += () => PasswordBox.Clear();
        viewModel.PasswordPreloaded += password => PasswordBox.Password = password;
        viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
    }

    private void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is DatabaseServerConfigViewModel viewModel && sender is PasswordBox box)
        {
            viewModel.SetPassword(box.Password);
        }
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(DatabaseServerConfigViewModel.IsConnectionSuccessful)
            || DataContext is not DatabaseServerConfigViewModel viewModel)
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
