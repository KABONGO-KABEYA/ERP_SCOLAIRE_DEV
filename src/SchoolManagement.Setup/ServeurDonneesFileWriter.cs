using System.IO;
using System.Text;

namespace SchoolManagement.Setup;

/// <summary>Écriture de ServeurDonnees.txt (Desktop + Api) avec MOTDEPASSE chiffré DPAPI.</summary>
internal static class ServeurDonneesFileWriter
{
    internal static void Write(string targetDirectory, InstallOptions opt)
    {
        var auth = opt.UseWindowsAuth ? "WINDOWS" : "SQL";
        var motDePasse = SetupDpapi.FormatMotDePasseForServeurDonnees(opt.UseWindowsAuth, opt.SqlPassword);

        var sb = new StringBuilder();
        sb.AppendLine("#######################################################");
        sb.AppendLine("# ERP SCOLAIRE RDC - genere par Setup");
        sb.AppendLine("#######################################################");
        sb.AppendLine($"SERVEUR={opt.SqlServer}");
        sb.AppendLine("PORT=1433");
        sb.AppendLine($"BASE={opt.Database}");
        sb.AppendLine($"AUTHENTIFICATION={auth}");
        sb.AppendLine($"UTILISATEUR={opt.SqlUser}");
        sb.AppendLine($"MOTDEPASSE={motDePasse}");
        File.WriteAllText(Path.Combine(targetDirectory, "ServeurDonnees.txt"), sb.ToString(), Encoding.UTF8);
    }
}
