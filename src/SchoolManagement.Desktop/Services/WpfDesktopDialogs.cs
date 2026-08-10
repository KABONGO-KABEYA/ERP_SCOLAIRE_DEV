using System.Windows;

namespace SchoolManagement.Desktop.Services;

public sealed class WpfDesktopDialogs : IDesktopDialogs
{
    public bool ConfirmYesNo(string message, string title) =>
        MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;

    public void SetClipboardText(string text) => Clipboard.SetText(text);
}
