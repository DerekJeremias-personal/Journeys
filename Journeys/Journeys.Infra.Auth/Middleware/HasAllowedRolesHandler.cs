using Microsoft.AspNetCore.Authorization;

namespace Journeys.Infra.Middleware
{
    public class HasAllowedRolesHandler : AuthorizationHandler<HasAllowedRolesRequirement>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            HasAllowedRolesRequirement requirement)
        {
            if (requirement.AllowedRoles.Count == 0)
                return Task.CompletedTask;

            var userRoles = JwtRoleClaimParser.GetRolesFromPrincipal(context.User, requirement.RolesClaimType);
            if (userRoles.Count == 0)
                return Task.CompletedTask;

            if (requirement.AllowedRoles.Any(allowed => userRoles.Contains(allowed)))
                context.Succeed(requirement);

            return Task.CompletedTask;
        }
    }
}
