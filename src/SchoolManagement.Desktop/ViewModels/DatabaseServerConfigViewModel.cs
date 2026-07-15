using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Configuration.Database;

namespace SchoolManagement.Desktop.ViewModels;

public partial class DatabaseServerConfigViewModel : ViewModelBase
{
    private readonly DatabaseConnectionBootstrap _bootstrap;
    private string _plainPassword = string.Empty;

    public DatabaseServerConfigViewModel(DatabaseConnectionBootstrap bootstrap, string? initialErrorMessage = null)
    {
        _bootstrap = bootstrap;
        ConnectionStatusMessage = initialErrorMessage;
        LoadFromFile();
    }

    public event Action<bool>? RequestClose;

    [ObservableProperty] private string _serveur = string.Empty;
    [ObservableProperty] private string _port = "1433";
    [ObservableProperty] private string _base = string.Empty;
    [ObservableProperty] private string _utilisateur = string.Empty;
    [ObservableProperty] private string? _serveurError;
    [ObservableProperty] private string? _portError;
    [ObservableProperty] private string? _baseError;
    [ObservableProperty] private string? _utilisateurError;
    [ObservableProperty] private string? _motDePasseError;
    [ObservableProperty] private string? _connectionStatusMessage;
    [ObservableProperty] private bool? _isConnectionSuccessful;
    [ObservableProperty] private bool _isBusy;

    public string AuthenticationLabel => "SQL Server";

    public void SetPassword(string password) => _plainPassword = password;

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (!ValidateForm())
        {
            return;
        }

        IsBusy = true;
        ConnectionStatusMessage = "Test de connexion en cours…";
        IsConnectionSuccessful = null;

        try
        {
            var configuration = BuildConfiguration();
            var result = await _bootstrap.ConnectionTester.TestConnectionAsync(configuration);
            ConnectionStatusMessage = result.Message;
            IsConnectionSuccessful = result.IsSuccess;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!ValidateForm())
        {
            return;
        }

        IsBusy = true;
        try
        {
            var configuration = BuildConfiguration();
            _bootstrap.ConfigurationManager.SaveConfiguration(configuration, _plainPassword);

            var reloaded = _bootstrap.LoadConfiguration();
            var testResult = await _bootstrap.ConnectionTester.TestConnectionAsync(reloaded);
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
        _plainPassword = string.Empty;
        LoadFromFile();
        PasswordResetRequested?.Invoke();
    }

    public event Action? PasswordResetRequested;

    public event Action<string>? PasswordPreloaded;

    private void LoadFromFile()
    {
        var configuration = _bootstrap.ConfigurationManager.LoadConfigurationWithoutPassword();
        Serveur = configuration.Serveur;
        Port = configuration.Port.ToString();
        Base = configuration.Base;
        Utilisateur = configuration.Utilisateur;

        if (string.IsNullOrWhiteSpace(_plainPassword))
        {
            try
            {
                var withPassword = _bootstrap.LoadConfiguration();
                _plainPassword = withPassword.MotDePasse;
                PasswordPreloaded?.Invoke(_plainPassword);
            }
            catch
            {
                _plainPassword = string.Empty;
            }
        }
    }

    private DatabaseConfiguration BuildConfiguration() =>
        new()
        {
            Serveur = Serveur.Trim(),
            Port = int.TryParse(Port, out var port) ? port : 0,
            Base = Base.Trim(),
            Authentification = DatabaseAuthenticationMode.SqlServer,
            Utilisateur = Utilisateur.Trim(),
            MotDePasse = _plainPassword
        };

    private bool ValidateForm()
    {
        ClearErrors();
        var configuration = BuildConfiguration();
        var validation = _bootstrap.ConfigurationManager.Validate(configuration, _plainPassword);

        foreach (var error in validation.FieldErrors)
        {
            switch (error.Key.ToUpperInvariant())
            {
                case "SERVEUR":
                    ServeurError = error.Value;
                    break;
                case "PORT":
                    PortError = error.Value;
                    break;
                case "BASE":
                    BaseError = error.Value;
                    break;
                case "UTILISATEUR":
                    UtilisateurError = error.Value;
                    break;
                case "MOTDEPASSE":
                    MotDePasseError = error.Value;
                    break;
            }
        }

        return validation.IsValid;
    }

    private void ClearErrors()
    {
        ServeurError = null;
        PortError = null;
        BaseError = null;
        UtilisateurError = null;
        MotDePasseError = null;
    }
}
