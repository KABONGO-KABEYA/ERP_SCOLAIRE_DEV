using System.Windows;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public partial class StudentDossierEditWindow : Window
{
    private readonly StudentDossierEditViewModel _viewModel;
    private readonly Guid _studentId;

    public StudentDossierEditWindow(StudentDossierEditViewModel viewModel, Guid studentId)
    {
        _viewModel = viewModel;
        _studentId = studentId;
        InitializeComponent();
        DataContext = viewModel;
        _viewModel.CloseRequested += OnCloseRequested;
        Loaded += OnWindowLoadedAsync;
    }

    private async void OnWindowLoadedAsync(object sender, RoutedEventArgs e)
    {
        Loaded -= OnWindowLoadedAsync;
        await _viewModel.LoadAsync(_studentId);

        if (_viewModel.IsDossierLoaded)
        {
            return;
        }

        MessageBox.Show(
            _viewModel.ValidationMessage ?? "Impossible de charger le dossier de l'élève.",
            "Modification du dossier",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        DialogResult = false;
        Close();
    }

    private void OnCloseRequested(object? sender, bool success)
    {
        DialogResult = success;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.CloseRequested -= OnCloseRequested;
        base.OnClosed(e);
    }
}
