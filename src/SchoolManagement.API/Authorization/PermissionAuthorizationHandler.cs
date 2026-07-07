namespace SchoolManagement.API.Authorization;

using Microsoft.AspNetCore.Authorization;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Shared.Constants;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly ICurrentUserService _currentUser;

    public PermissionAuthorizationHandler(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (_currentUser.HasPermission(requirement.Permission)
            || _currentUser.HasPermission(Permissions.AdminFull))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
