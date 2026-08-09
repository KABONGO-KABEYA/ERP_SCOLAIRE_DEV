using SchoolManagement.Shared.Constants;

namespace SchoolManagement.Desktop.Services;

/// <summary>
/// Contrôle d'accès UI Phase 4 — permissions JWT uniquement (pas de rôle ADMIN codé en dur).
/// </summary>
public static class SessionPermissions
{
    public static bool Can(IAuthSessionService session, string permissionCode) =>
        session.HasPermission(permissionCode);

    public static bool CanAny(IAuthSessionService session, params string[] permissionCodes)
    {
        foreach (var code in permissionCodes)
        {
            if (session.HasPermission(code))
            {
                return true;
            }
        }

        return false;
    }

    public static bool CanAll(IAuthSessionService session, params string[] permissionCodes)
    {
        foreach (var code in permissionCodes)
        {
            if (!session.HasPermission(code))
            {
                return false;
            }
        }

        return permissionCodes.Length > 0;
    }
}
