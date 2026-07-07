using System.Windows;
using System.Windows.Controls;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public partial class ChangePasswordWindow : Window
{
    public ChangePasswordWindow(ChangePasswordViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.PasswordChanged += () =>
        {
            DialogResult = true;
            Close();
        };

        if (viewModel.IsMandatory)
        {
            Closing += (_, e) =>
            {
                if (DialogResult != true)
                {
                    e.Cancel = true;
                }
            };
        }
    }

    private void CurrentPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is ChangePasswordViewModel vm && sender is PasswordBox box)
        {
            vm.CurrentPassword = box.Password;
        }
    }

    private void NewPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is ChangePasswordViewModel vm && sender is PasswordBox box)
        {
            vm.NewPassword = box.Password;
        }
    }

    private void ConfirmPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is ChangePasswordViewModel vm && sender is PasswordBox box)
        {
            vm.ConfirmPassword = box.Password;
        }
    }
}
