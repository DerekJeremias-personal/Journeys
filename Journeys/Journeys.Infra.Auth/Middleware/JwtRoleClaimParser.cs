using System.Security.Claims;
using System.Text.Json;

namespace Journeys.Infra.Middleware
{
    internal static class JwtRoleClaimParser
    {
        /// <summary>
        /// Collects role strings from JWT claims: repeated claims, a JSON array string, or a single role value.
        /// </summary>
        public static HashSet<string> GetRolesFromPrincipal(ClaimsPrincipal user, string rolesClaimType)
        {
            var roles = new HashSet<string>(StringComparer.Ordinal);
            foreach (var claim in user.FindAll(c => c.Type == rolesClaimType))
            {
                foreach (var r in ParseRoleClaimValue(claim.Value))
                    roles.Add(r);
            }

            return roles;
        }

        private static List<string> ParseRoleClaimValue(string? raw)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(raw))
                return result;

            var v = raw.Trim();
            if (v.StartsWith('[') && v.EndsWith(']'))
            {
                try
                {
                    var list = JsonSerializer.Deserialize<List<string>>(v);
                    if (list != null)
                    {
                        foreach (var item in list)
                        {
                            if (!string.IsNullOrWhiteSpace(item))
                                result.Add(item.Trim());
                        }

                        return result;
                    }
                }
                catch (JsonException)
                {
                    // treat as literal single value
                }
            }

            result.Add(v);
            return result;
        }
    }
}
