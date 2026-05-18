using System.Security.Claims;
using CurriculosProIA.Repository.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace CurriculosProIA.Api.Authorization;

public class AdminAuthorizationHandler : AuthorizationHandler<AdminRequirement>
{
    private readonly IUserProfileRepository _users;

    public AdminAuthorizationHandler(IUserProfileRepository users)
    {
        _users = users;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminRequirement requirement)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("userId");

        if (string.IsNullOrEmpty(userId))
            return;

        var profile = await _users.GetUserProfileAsync(userId);
        if (profile == null)
            return;

        if (string.Equals(profile.UserType, "admin", StringComparison.OrdinalIgnoreCase))
            context.Succeed(requirement);
    }
}
