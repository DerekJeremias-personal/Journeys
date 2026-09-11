using Microsoft.AspNetCore.Authorization;

namespace Journeys.Infra.Middleware
{
    public class HasAllowedRolesRequirement : IAuthorizationRequirement
    {
        public string RolesClaimType { get; }
        public IReadOnlyList<string> AllowedRoles { get; }

        public HasAllowedRolesRequirement(string rolesClaimType, IReadOnlyList<string> allowedRoles)
        {
            RolesClaimType = rolesClaimType ?? throw new ArgumentNullException(nameof(rolesClaimType));
            AllowedRoles = allowedRoles ?? throw new ArgumentNullException(nameof(allowedRoles));
        }
    }
}
