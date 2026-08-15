using System.Security.AccessControl;
using System.Security.Principal;

namespace SchoolManagement.UpdateAgent;

public static class AgentAcl
{
    public static void RestrictDirectory(string path, string accountName)
    {
        Directory.CreateDirectory(path);
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var info = new DirectoryInfo(path);
        var security = info.GetAccessControl();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        security.AddAccessRule(new FileSystemAccessRule(
            system,
            FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            accountName,
            FileSystemRights.Modify,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));
        info.SetAccessControl(security);
    }
}
