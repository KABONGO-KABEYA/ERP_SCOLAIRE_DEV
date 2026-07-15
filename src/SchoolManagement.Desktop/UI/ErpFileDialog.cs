using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;

namespace SchoolManagement.Desktop.UI;

public static class ErpFileDialog
{
    static ErpFileDialog()
    {
        ProcessEnvironmentNormalizer.Apply();
    }

    public static bool? ShowOpen(OpenFileDialog dialog, Window? owner = null)
    {
        PrepareOpen(dialog);
        return owner is not null ? dialog.ShowDialog(owner) : dialog.ShowDialog();
    }

    public static bool? ShowSave(SaveFileDialog dialog, Window? owner = null)
    {
        PrepareSave(dialog);
        return owner is not null ? dialog.ShowDialog(owner) : dialog.ShowDialog();
    }

    public static void PrepareOpen(OpenFileDialog dialog)
    {
        ProcessEnvironmentNormalizer.Apply();
        ApplySafeInitialDirectory(dialog);
        dialog.RestoreDirectory = false;
        dialog.CheckFileExists = true;
        dialog.ValidateNames = true;
        dialog.AddToRecent = false;
    }

    public static void PrepareSave(SaveFileDialog dialog)
    {
        ProcessEnvironmentNormalizer.Apply();
        ApplySafeInitialDirectory(dialog);
        dialog.RestoreDirectory = false;
        dialog.ValidateNames = true;
        dialog.AddToRecent = false;
    }

    public static Window? ResolveOwnerWindow() =>
        System.Windows.Application.Current?.Windows
            .OfType<Window>()
            .FirstOrDefault(window => window.IsActive)
        ?? System.Windows.Application.Current?.MainWindow;

    private static void ApplySafeInitialDirectory(FileDialog dialog)
    {
        var folder = ResolveSafeInitialDirectory();
        dialog.InitialDirectory = folder;

        // Ancrer le dialogue sur un dossier valide (évite le MRU Windows vers build_home\Desktop).
        if (dialog is OpenFileDialog)
        {
            dialog.FileName = string.Empty;
        }
    }

    private static string ResolveSafeInitialDirectory()
    {
        var userName = Environment.UserName;
        if (!string.IsNullOrWhiteSpace(userName))
        {
            foreach (var folder in new[]
                     {
                         Path.Combine(@"C:\Users", userName, "Pictures"),
                         Path.Combine(@"C:\Users", userName, "Desktop"),
                         Path.Combine(@"C:\Users", userName, "Documents"),
                         Path.Combine(@"C:\Users", userName, "Downloads"),
                     })
            {
                if (IsUsableDirectory(folder))
                {
                    return folder;
                }
            }
        }

        var realProfile = ProcessEnvironmentNormalizer.ResolveRealUserProfileDirectory();
        if (!string.IsNullOrWhiteSpace(realProfile))
        {
            foreach (var sub in new[] { "Pictures", "Desktop", "Documents", "Downloads" })
            {
                var folder = Path.Combine(realProfile, sub);
                if (IsUsableDirectory(folder))
                {
                    return folder;
                }
            }

            if (IsUsableDirectory(realProfile))
            {
                return realProfile;
            }
        }

        foreach (var folder in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                     Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                     Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                 })
        {
            if (IsUsableDirectory(folder))
            {
                return folder;
            }
        }

        return Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
    }

    private static bool IsUsableDirectory(string? folder) =>
        !string.IsNullOrWhiteSpace(folder)
        && Directory.Exists(folder)
        && !folder.Contains("build_home", StringComparison.OrdinalIgnoreCase);
}
