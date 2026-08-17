using System.IO;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace SchoolManagement.Setup;

public partial class MainWindow : Window
{
    private int _step; // 1..5
    private readonly InstallSessionState _installSession = new();
    private bool _sqlOk;
    private bool _cloudOk;
    private bool _storageOk;

    public MainWindow()
    {
        InitializeComponent();
        TxtInstallRoot.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "ERP Scolaire");

        foreach (var instance in InstallerEngine.DetectSqlInstances())
            CmbSql.Items.Add(instance);
        if (CmbSql.Items.Count > 0)
            CmbSql.SelectedIndex = 0;

        try
        {
            _ = InstallerEngine.FindPayloadRoot();
            Log("Payload détecté — assistant prêt.");
        }
        catch (Exception ex)
        {
            Log("ATTENTION : " + ex.Message);
        }

        ShowStep(1);
    }

    private bool IsServer => RbServer.IsChecked == true;

    private void ShowStep(int step)
    {
        _step = step;
        Step1.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
        Step2.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
        Step3.Visibility = step == 3 ? Visibility.Visible : Visibility.Collapsed;
        Step4.Visibility = step == 4 ? Visibility.Visible : Visibility.Collapsed;
        Step5.Visibility = step == 5 ? Visibility.Visible : Visibility.Collapsed;

        BtnBack.IsEnabled = step > 1 && !_installSession.IsBusy && !_installSession.IsCompleted;

        TxtStepTitle.Text = step switch
        {
            1 => "Étape 1 — Type d'installation",
            2 => "Étape 2 — SQL Server local",
            3 => "Étape 3 — Serveur Cloud",
            4 => "Étape 4 — Serveur de fichiers",
            5 => IsServer ? "Étape 5 — Vérification et installation" : "Étape 2 — Installation client",
            _ => ""
        };

        if (step == 5)
        {
            PnlClientUrl.Visibility = IsServer ? Visibility.Collapsed : Visibility.Visible;
            ChkVirgin.Visibility = IsServer ? Visibility.Visible : Visibility.Collapsed;
            TxtRecap.Text = BuildRecap();
            BtnNext.Content = _installSession.PrimaryButtonLabel(step, IsServer);
        }
        else
        {
            BtnNext.Content = "Suivant";
        }

        UpdateAuthUi();
    }

    private void RefreshNextButton()
    {
        if (_step == 5)
            BtnNext.Content = _installSession.PrimaryButtonLabel(_step, IsServer);
    }

    private string BuildRecap()
    {
        if (!IsServer)
            return $"Client Desktop → {TxtInstallRoot.Text}\nAPI : {TxtApiUrl.Text}";

        return
            $"Serveur → {TxtInstallRoot.Text}\n" +
            $"SQL local : {CmbSql.Text} / {TxtDatabase.Text}\n" +
            $"Cloud : {(ChkCloud.IsChecked == true ? $"{TxtCloudServer.Text}:{TxtCloudPort.Text} / {TxtCloudDatabase.Text}" : "désactivé")}\n" +
            $"Fichiers : {TxtStorageRoot.Text}\n" +
            "Après Terminer : service API, sync cloud, base vierge, Desktop.";
    }

    private void Auth_Changed(object sender, RoutedEventArgs e) => UpdateAuthUi();

    private void UpdateAuthUi()
    {
        if (TxtSqlUser is null) return;
        var sql = RbSqlAuth.IsChecked == true;
        var vis = sql ? Visibility.Visible : Visibility.Collapsed;
        LblSqlUser.Visibility = vis;
        TxtSqlUser.Visibility = vis;
        LblSqlPwd.Visibility = vis;
        TxtSqlPassword.Visibility = vis;
    }

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Dossier d'installation" };
        if (dlg.ShowDialog() == true)
            TxtInstallRoot.Text = dlg.FolderName;
    }

    private void BtnBrowseStorage_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Dossier de stockage documents" };
        if (dlg.ShowDialog() == true)
            TxtStorageRoot.Text = dlg.FolderName;
    }

    private async void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        if (_installSession.IsBusy || _installSession.IsCompleted) return;
        if (!IsServer)
        {
            ShowStep(1);
            return;
        }

        ShowStep(Math.Max(1, _step - 1));
        await Task.CompletedTask;
    }

    private async void BtnNext_Click(object sender, RoutedEventArgs e)
    {
        if (_installSession.IsBusy) return;

        if (_step == 5 && _installSession.IsCompleted)
        {
            Close();
            return;
        }

        if (_step == 1)
        {
            if (string.IsNullOrWhiteSpace(TxtInstallRoot.Text))
            {
                MessageBox.Show("Indiquez le dossier d'installation.", "Setup", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!IsServer)
            {
                ShowStep(5); // client : récap direct
                return;
            }

            ShowStep(2);
            return;
        }

        if (_step == 2)
        {
            if (!_sqlOk)
            {
                MessageBox.Show("Testez et validez la connexion SQL locale avant de continuer.", "Setup",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ShowStep(3);
            return;
        }

        if (_step == 3)
        {
            if (ChkCloud.IsChecked == true && !_cloudOk)
            {
                MessageBox.Show("Testez et validez la connexion Cloud avant de continuer.", "Setup",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ShowStep(4);
            return;
        }

        if (_step == 4)
        {
            if (!_storageOk)
            {
                MessageBox.Show("Testez l'accès au dossier de fichiers avant de continuer.", "Setup",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ShowStep(5);
            return;
        }

        if (_step == 5)
            await RunInstallAsync();
    }

    private InstallOptions BuildOptions()
    {
        _ = int.TryParse(TxtCloudPort.Text.Trim(), out var cloudPort);
        if (cloudPort <= 0) cloudPort = 1433;

        return new InstallOptions
        {
            Role = IsServer ? InstallRole.Server : InstallRole.Client,
            InstallRoot = TxtInstallRoot.Text.Trim(),
            SqlServer = (CmbSql.Text ?? "").Trim(),
            Database = TxtDatabase.Text.Trim(),
            UseWindowsAuth = RbWinAuth.IsChecked == true,
            SqlUser = TxtSqlUser.Text.Trim(),
            SqlPassword = TxtSqlPassword.Password,
            SqlConnectionVerified = _sqlOk,
            ConfigureCloudSync = IsServer && ChkCloud.IsChecked == true,
            CloudSqlServer = TxtCloudServer.Text.Trim(),
            CloudSqlPort = cloudPort,
            CloudDatabase = TxtCloudDatabase.Text.Trim(),
            CloudSqlUser = TxtCloudUser.Text.Trim(),
            CloudSqlPassword = TxtCloudPassword.Password,
            CloudConnectionVerified = _cloudOk,
            StorageRoot = TxtStorageRoot.Text.Trim(),
            CreateNetworkShare = ChkShare.IsChecked == true,
            ApiBaseUrl = TxtApiUrl.Text.Trim(),
            CreateDesktopShortcut = ChkShortcut.IsChecked == true,
            StartAfterInstall = ChkStart.IsChecked == true,
            ApplyVirginDatabase = ChkVirgin.IsChecked == true,
        };
    }

    private async void BtnTestSql_Click(object sender, RoutedEventArgs e)
    {
        if (_installSession.IsBusy || _installSession.IsCompleted) return;
        SetBusy(true);
        _sqlOk = false;
        TxtSqlStatus.Text = "Test…";
        TxtSqlStatus.Foreground = Brushes.Gray;
        try
        {
            var engine = new InstallerEngine(Log);
            var server = await engine.TestSqlAsync(BuildOptions());
            _sqlOk = true;
            TxtSqlStatus.Text = "OK — " + server;
            TxtSqlStatus.Foreground = Brushes.DarkGreen;
            Log("SQL local OK : " + server);
        }
        catch (Exception ex)
        {
            TxtSqlStatus.Text = "ÉCHEC";
            TxtSqlStatus.Foreground = Brushes.DarkRed;
            Log("SQL local : " + ex.Message);
            MessageBox.Show(ex.Message, "Échec SQL local", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void BtnTestCloud_Click(object sender, RoutedEventArgs e)
    {
        if (_installSession.IsBusy || _installSession.IsCompleted) return;
        SetBusy(true);
        _cloudOk = false;
        TxtCloudStatus.Text = "Test…";
        TxtCloudStatus.Foreground = Brushes.Gray;
        try
        {
            var engine = new InstallerEngine(Log);
            var info = await engine.TestCloudSqlAsync(BuildOptions());
            _cloudOk = true;
            TxtCloudStatus.Text = "OK — " + info;
            TxtCloudStatus.Foreground = Brushes.DarkGreen;
            Log("SQL cloud OK : " + info);
        }
        catch (Exception ex)
        {
            TxtCloudStatus.Text = "ÉCHEC";
            TxtCloudStatus.Foreground = Brushes.DarkRed;
            Log("SQL cloud : " + ex.Message);
            MessageBox.Show(ex.Message, "Échec SQL cloud", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void BtnTestStorage_Click(object sender, RoutedEventArgs e)
    {
        if (_installSession.IsBusy || _installSession.IsCompleted) return;
        SetBusy(true);
        _storageOk = false;
        TxtStorageStatus.Text = "Test…";
        TxtStorageStatus.Foreground = Brushes.Gray;
        try
        {
            var path = TxtStorageRoot.Text.Trim();
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
                throw new InvalidOperationException("Indiquez un chemin absolu (ex. D:\\ERP_SCOLAIRE).");

            var engine = new InstallerEngine(Log);
            await engine.ProbeStorageAsync(path);
            _storageOk = true;
            TxtStorageStatus.Text = "OK — dossier accessible en écriture";
            TxtStorageStatus.Foreground = Brushes.DarkGreen;
            Log("Stockage OK : " + path);
        }
        catch (Exception ex)
        {
            TxtStorageStatus.Text = "ÉCHEC";
            TxtStorageStatus.Foreground = Brushes.DarkRed;
            Log("Stockage : " + ex.Message);
            MessageBox.Show(ex.Message, "Échec dossier fichiers", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task RunInstallAsync()
    {
        if (!_installSession.CanStartInstall)
            return;

        var opt = BuildOptions();
        if (IsServer)
        {
            if (!_sqlOk || (opt.ConfigureCloudSync && !_cloudOk) || !_storageOk)
            {
                MessageBox.Show(
                    "Tous les tests obligatoires doivent réussir avant Terminer (SQL, Cloud si activé, Fichiers).",
                    "Setup", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        var confirm = MessageBox.Show(
            IsServer
                ? "Lancer l'installation SERVEUR complète (API, service, cloud, fichiers, base vierge) ?"
                : "Lancer l'installation CLIENT ?",
            "Confirmer", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        SetBusy(true);
        try
        {
            var engine = new InstallerEngine(Log);
            await engine.InstallAsync(opt);
            _installSession.MarkCompleted();
            Log("Installation réussie — fermeture de l'assistant.");
            MessageBox.Show(
                "Installation terminée avec succès.\n\nAu premier lancement Desktop, un assistant vous demandera les informations de l'établissement.",
                "ERP Scolaire", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex)
        {
            Log("ERREUR : " + ex);
            MessageBox.Show(ex.Message, "Échec installation", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false);
            RefreshNextButton();
        }
    }

    private void SetBusy(bool busy)
    {
        _installSession.SetBusy(busy);
        BtnNext.IsEnabled = !busy || _installSession.IsCompleted;
        BtnBack.IsEnabled = !busy && _step > 1 && !_installSession.IsCompleted;
    }

    private void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        Dispatcher.Invoke(() =>
        {
            TxtLog.AppendText(line + Environment.NewLine);
            TxtLog.ScrollToEnd();
        });
    }
}
