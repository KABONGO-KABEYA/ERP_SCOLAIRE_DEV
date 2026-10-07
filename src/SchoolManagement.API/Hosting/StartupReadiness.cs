namespace SchoolManagement.API.Hosting;

/// <summary>
/// Indique si le démarrage (schéma + seed) est terminé.
/// Le host Kestrel / service Windows démarre avant ce travail pour éviter le timeout SCM 1053 (30 s).
/// </summary>
public sealed class StartupReadiness
{
    private volatile bool _isReady;

    public bool IsReady => _isReady;

    public void MarkReady() => _isReady = true;
}
