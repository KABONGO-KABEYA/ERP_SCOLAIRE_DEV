using System.IO;
using SchoolManagement.Application.Schools.DTOs;
namespace SchoolManagement.Desktop.Services;

public static class SubscriptionExpiryReminder
{
    public static string? GetMessage(SchoolSubscriptionDto? subscription, DateTime utcNow, TimeZoneInfo? timeZone = null)
    {
        if (subscription is not { IsValid: true, IsActive: true, ExpirationDate: { } expiry }
            || expiry <= utcNow) return null;
        timeZone ??= TimeZoneInfo.Local;
        var expiration = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(expiry, DateTimeKind.Utc), timeZone);
        var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), timeZone).Date;
        var days = (expiration.Date - today).Days;
        if (days < 0 || days > 3) return null;
        var remaining = days == 0 ? "aujourd’hui" : days == 1 ? "demain" : $"dans {days} jours";
        return $"Votre abonnement expire {remaining}, le {expiration:dd/MM/yyyy} à {expiration:HH:mm}. Pensez à le renouveler pour continuer à utiliser l’ERP.";
    }

    public static bool ShouldShow(string? previousDay, DateTime localNow) => previousDay != localNow.ToString("yyyy-MM-dd");

    public static void ShowOncePerDay(SchoolSubscriptionDto? subscription, Guid userId, System.Windows.Window owner)
    {
        var message = GetMessage(subscription, DateTime.UtcNow);
        if (message is null) return;
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ERP_Scolaire", "SubscriptionReminders");
        var file = Path.Combine(directory, $"{subscription!.SchoolId:N}-{userId:N}.txt");
        try
        {
            if (File.Exists(file) && !ShouldShow(File.ReadAllText(file).Trim(), DateTime.Now)) return;
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        System.Windows.MessageBox.Show(owner, message, "Renouvellement de l’abonnement", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        try { Directory.CreateDirectory(directory); File.WriteAllText(file, DateTime.Now.ToString("yyyy-MM-dd")); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
