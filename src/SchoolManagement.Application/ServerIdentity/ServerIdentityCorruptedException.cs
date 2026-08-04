namespace SchoolManagement.Application.ServerIdentity;

/// <summary>
/// <see cref="ServerIdentity"/> fichier présent mais illisible, incohérent ou corrompu.
/// Aucune régénération automatique : restaurer <c>ServerIdentity.json</c> ou la sauvegarde <c>.bak</c>.
/// </summary>
public sealed class ServerIdentityCorruptedException : InvalidOperationException
{
    public ServerIdentityCorruptedException(string message)
        : base(message)
    {
    }

    public ServerIdentityCorruptedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
