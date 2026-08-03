using System.Windows;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public partial class DeliberationDecisionDialog : Window
{
    private readonly DeliberationDecisionDialogViewModel _viewModel;

    public DeliberationDecisionDialog(DeliberationDecisionDialogViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        // Force refresh of conditional panels after load.
        _viewModel.SelectedFinalDecision = _viewModel.SelectedFinalDecision;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.TryBuildSaveRequest())
        {
            return;
        }

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
