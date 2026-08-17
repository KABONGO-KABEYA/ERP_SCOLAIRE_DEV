namespace SchoolManagement.Setup;

/// <summary>Empêche une réinstallation après succès et pilote le libellé du bouton Terminer/Fermer.</summary>
internal sealed class InstallSessionState
{
    public bool IsBusy { get; private set; }
    public bool IsCompleted { get; private set; }

    public bool CanStartInstall => !IsBusy && !IsCompleted;

    public void SetBusy(bool busy) => IsBusy = busy;

    public void MarkCompleted() => IsCompleted = true;

    public string PrimaryButtonLabel(int step, bool isServer) =>
        step == 5
            ? (IsCompleted ? "Fermer" : "Terminer")
            : "Suivant";
}
