namespace SchoolManagement.Desktop.Services;

/// <summary>Interactions UI testables (confirmations, presse-papiers).</summary>
public interface IDesktopDialogs
{
    bool ConfirmYesNo(string message, string title);

    void SetClipboardText(string text);
}
