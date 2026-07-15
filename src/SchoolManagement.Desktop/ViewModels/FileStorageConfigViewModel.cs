using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Configuration.FileStorage;

namespace SchoolManagement.Desktop.ViewModels;

public partial class FileStorageConfigViewModel : ViewModelBase
{
    private readonly FileStorageConfigurationManager _configurationManager;
    private readonly FileStoragePathTester _pathTester;

    public FileStorageConfigViewModel(
        FileStorageConfigurationManager configurationManager,
        FileStoragePathTester pathTester,
        string? initialErrorMessage = null)
    {
        _configurationManager = configurationManager;
        _pathTester = pathTester;
        ConnectionStatusMessage = initialErrorMessage;
        LoadFromFile();
    }

    public event Action<bool>? RequestClose;

    [ObservableProperty] private string _racine = string.Empty;
    [ObservableProperty] private string? _racineError;
    [ObservableProperty] private string? _connectionStatusMessage;
    [ObservableProperty] private bool? _isConnectionSuccessful;
    [ObservableProperty] private bool _isBusy;

    public void ApplyRacine(string racine)
    {
        Racine = racine.Trim();
        RacineError = null;
        IsConnectionSuccessful = null;
    }

    [RelayCommand]
    private void TestConnection()
    {
        RunPathTest(requireWriteAccess: false);
    }

    [RelayCommand]
    private void Save()
    {
        if (!ValidateForm())
        {
            return;
        }

        IsBusy = true;
        try
        {
            var configuration = BuildConfiguration();
            _configurationManager.SaveConfiguration(configuration);

            var testResult = _pathTester.TestConfiguration(
                configuration,
                AppContext.BaseDirectory,
                requireWriteAccess: false);

            ConnectionStatusMessage = testResult.Message;
            IsConnectionSuccessful = testResult.IsSuccess;

            if (testResult.IsSuccess)
            {
                RequestClose?.Invoke(true);
            }
        }
        catch (Exception ex)
        {
            ConnectionStatusMessage = ex.Message;
            IsConnectionSuccessful = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(false);

    [RelayCommand]
    private void Reset()
    {
        ClearErrors();
        ConnectionStatusMessage = null;
        IsConnectionSuccessful = null;
        Racine = string.Empty;
    }

    private void RunPathTest(bool requireWriteAccess)
    {
        if (!ValidateForm())
        {
            return;
        }

        IsBusy = true;
        ConnectionStatusMessage = "Test d'accès au dossier en cours…";
        IsConnectionSuccessful = null;

        try
        {
            var configuration = BuildConfiguration();
            var result = _pathTester.TestConfiguration(
                configuration,
                AppContext.BaseDirectory,
                requireWriteAccess);

            ConnectionStatusMessage = result.Message;
            IsConnectionSuccessful = result.IsSuccess;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void LoadFromFile()
    {
        if (!_configurationManager.IsConfigured())
        {
            Racine = string.Empty;
            return;
        }

        var configuration = _configurationManager.LoadConfiguration();
        Racine = configuration.Racine;
    }

    private FileStorageConfiguration BuildConfiguration() =>
        new() { Racine = Racine.Trim() };

    private bool ValidateForm()
    {
        ClearErrors();
        var validation = _configurationManager.Validate(BuildConfiguration());

        foreach (var error in validation.FieldErrors)
        {
            if (error.Key.Equals(nameof(FileStorageConfiguration.Racine), StringComparison.OrdinalIgnoreCase))
            {
                RacineError = error.Value;
            }
        }

        return validation.IsValid;
    }

    private void ClearErrors() => RacineError = null;
}
