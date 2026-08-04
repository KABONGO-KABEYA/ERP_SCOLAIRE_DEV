using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace SchoolManagement.Infrastructure.ServerIdentity;

/// <summary>
/// Restreint l'accès à <see cref="ServerIdentityFileStore.FileName"/> :
/// compte d'exécution de l'API, Administrateurs et SYSTEM (service Windows) uniquement.
/// </summary>
internal static class ServerIdentityFilePermissions
{
    public static void ApplyRestrictive(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            ApplyWindows(filePath);
        }
        else
        {
            ApplyUnix(filePath);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void ApplyWindows(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        var security = fileInfo.GetAccessControl(AccessControlSections.Access);

        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

        foreach (AuthorizationRule rule in security.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier)))
        {
            if (rule is FileSystemAccessRule fsRule)
            {
                security.RemoveAccessRule(fsRule);
            }
        }

        void Grant(WellKnownSidType sidType, FileSystemRights rights)
        {
            var sid = new SecurityIdentifier(sidType, null);
            security.AddAccessRule(new FileSystemAccessRule(
                sid,
                rights,
                AccessControlType.Allow));
        }

        Grant(WellKnownSidType.LocalSystemSid, FileSystemRights.FullControl);
        Grant(WellKnownSidType.BuiltinAdministratorsSid, FileSystemRights.FullControl);

        try
        {
            var processSid = WindowsIdentity.GetCurrent().User;
            if (processSid is not null)
            {
                security.AddAccessRule(new FileSystemAccessRule(
                    processSid,
                    FileSystemRights.FullControl,
                    AccessControlType.Allow));
            }
        }
        catch
        {
            // Démarrage sans identité Windows (tests) : SYSTEM + Administrateurs suffisent.
        }

        fileInfo.SetAccessControl(security);
    }

    private static void ApplyUnix(string filePath)
    {
        try
        {
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                File.SetUnixFileMode(filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }
        catch
        {
            // Environnement restreint (conteneur, tests) : ne pas bloquer le démarrage.
        }
    }
}
