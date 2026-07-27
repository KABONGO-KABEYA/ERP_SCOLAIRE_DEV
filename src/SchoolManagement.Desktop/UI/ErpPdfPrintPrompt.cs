using System.Diagnostics;
using System.Windows;

namespace SchoolManagement.Desktop.UI;

/// <summary>
/// Après génération d'un PDF : demande systématiquement si l'utilisateur veut imprimer.
/// </summary>
public static class ErpPdfPrintPrompt
{
    public static bool AskAndPrintIfRequested(string pdfPath, string? documentTitle = null)
    {
        if (string.IsNullOrWhiteSpace(pdfPath) || !System.IO.File.Exists(pdfPath))
        {
            return false;
        }

        var owner = ErpFileDialog.ResolveOwnerWindow();
        var title = string.IsNullOrWhiteSpace(documentTitle) ? "Impression PDF" : documentTitle;
        var result = owner is null
            ? MessageBox.Show(
                "Le PDF a été généré et enregistré.\n\nVoulez-vous l'imprimer maintenant ?",
                title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question)
            : MessageBox.Show(
                owner,
                "Le PDF a été généré et enregistré.\n\nVoulez-vous l'imprimer maintenant ?",
                title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo(pdfPath)
            {
                UseShellExecute = true,
                Verb = "print"
            });
            return true;
        }
        catch (Exception ex)
        {
            if (owner is null)
            {
                MessageBox.Show(
                    $"Impossible d'envoyer le PDF à l'impression.\n{ex.Message}",
                    title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            else
            {
                MessageBox.Show(
                    owner,
                    $"Impossible d'envoyer le PDF à l'impression.\n{ex.Message}",
                    title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            return false;
        }
    }
}
