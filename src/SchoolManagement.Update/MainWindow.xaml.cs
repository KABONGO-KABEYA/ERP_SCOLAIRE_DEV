using System.Windows;
using System.Windows.Threading;

namespace SchoolManagement.Update;

public partial class MainWindow : Window
{
    private readonly UpdateEngine _engine = new();
    private DetectedInstallation? _installation;
    private bool _busy;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _ = UpdateEngine.FindPayloadRoot();
            _installation = _engine.DetectInstallation();
            TxtSummary.Text =
                $"Installation détectée : {_installation.InstallRoot}{Environment.NewLine}" +
                $"Rôle : {_installation.Role}{Environment.NewLine}" +
                $"Version installée : {_installation.PreviousVersion}{Environment.NewLine}" +
                $"Version cible : {InstalledVersionReader.UpdatePackageVersion}{Environment.NewLine}" +
                $"Référence Setup : {InstalledVersionReader.LastSetupReferenceVersion} ({InstalledVersionReader.ReferenceSetupCommit})";
            Log("Prêt — aucune modification tant que vous n'avez pas lancé la mise à jour.");
        }
        catch (Exception ex)
        {
            TxtSummary.Text = "Installation ou payload invalide.";
            Log("ERREUR : " + ex.Message);
            BtnUpdate.IsEnabled = false;
        }
    }

    private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _installation is null)
            return;

        var confirm = MessageBox.Show(
            "Cette opération va mettre à jour l'API et le Desktop sans toucher aux données SQL existantes.\n\n" +
            "Les anciens binaires seront sauvegardés pour rollback.\n\nContinuer ?",
            "Confirmation",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;

        _busy = true;
        BtnUpdate.IsEnabled = false;
        TxtLog.Clear();

        try
        {
            var report = await _engine.RunAsync(_installation);
            foreach (var line in report.LogLines)
                Log(line);

            Log("");
            Log(report.FormatSummary());
            MessageBox.Show(
                report.FormatSummary(),
                report.Success ? "Mise à jour terminée" : "Mise à jour échouée",
                MessageBoxButton.OK,
                report.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            Log("ERREUR FATALE : " + ex.Message);
            MessageBox.Show(ex.Message, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _busy = false;
            BtnUpdate.IsEnabled = true;
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    private void Log(string message)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => Log(message));
            return;
        }

        TxtLog.AppendText(message + Environment.NewLine);
        TxtLog.ScrollToEnd();
    }
}
