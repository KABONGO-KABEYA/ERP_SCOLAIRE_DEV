using System.Globalization;
using System.Windows.Data;
using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.UI;

public sealed class CurrentUserRoleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var session = App.Services?.GetService<IAuthSessionService>();
        var roles = session?.CurrentUser?.Roles;
        if (roles is null || roles.Count == 0)
        {
            return "Utilisateur";
        }

        return roles[0];
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
