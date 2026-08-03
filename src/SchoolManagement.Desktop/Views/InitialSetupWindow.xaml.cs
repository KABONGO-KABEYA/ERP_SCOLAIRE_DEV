using System.Windows;
using System.Windows.Controls;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public partial class InitialSetupWindow : Window
{
    private readonly InitialSetupViewModel _viewModel;

    public InitialSetupWindow(InitialSetupViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(InitialSetupViewModel.Step) or nameof(InitialSetupViewModel.IsBusy))
                SyncStepUi();
        };
        viewModel.Completed += () =>
        {
            DialogResult = true;
            Close();
        };
        SyncStepUi();
    }

    private void SyncStepUi()
    {
        PanelStep1.Visibility = _viewModel.Step == 1 ? Visibility.Visible : Visibility.Collapsed;
        PanelStep2.Visibility = _viewModel.Step == 2 ? Visibility.Visible : Visibility.Collapsed;
        PanelStep3.Visibility = _viewModel.Step == 3 ? Visibility.Visible : Visibility.Collapsed;
        PanelStep4.Visibility = _viewModel.Step == 4 ? Visibility.Visible : Visibility.Collapsed;
        BtnNext.Visibility = _viewModel.Step < 4 ? Visibility.Visible : Visibility.Collapsed;
        BtnFinish.Visibility = _viewModel.Step == 4 ? Visibility.Visible : Visibility.Collapsed;
        BtnNext.IsEnabled = !_viewModel.IsBusy;
        BtnFinish.IsEnabled = !_viewModel.IsBusy;
    }

    private void BtnNext_OnClick(object sender, RoutedEventArgs e) => _viewModel.NextCommand.Execute(null);

    private void PwdAdmin_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox box)
            _viewModel.AdminPassword = box.Password;
    }

    private void PwdAdminConfirm_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox box)
            _viewModel.AdminPasswordConfirm = box.Password;
    }
}
