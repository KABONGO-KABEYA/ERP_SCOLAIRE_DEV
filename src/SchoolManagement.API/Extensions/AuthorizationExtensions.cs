namespace SchoolManagement.API.Extensions;

using Microsoft.AspNetCore.Authorization;
using SchoolManagement.API.Authorization;
using SchoolManagement.Shared.Constants;

public static class AuthorizationExtensions
{
    public static IServiceCollection AddPermissionPolicies(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();

        services.AddAuthorization(options =>
        {
            // Pré-enregistre les aliases connus ; le provider dynamique couvre le reste (BD).
            foreach (var permission in Permissions.All)
            {
                options.AddPolicy(permission, policy =>
                    policy.Requirements.Add(new PermissionRequirement(permission)));
            }
        });

        return services;
    }
}
