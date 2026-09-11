using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.Utility
{
    public static class SqlValidator
    {
        public static bool Validate(string sql, out string error)
        {
            error = string.Empty;

            var normalized = sql.Trim().ToLowerInvariant();

            // Only SELECT / WITH
            if (!(normalized.StartsWith("select") || normalized.StartsWith("with")))
            {
                error = "Only SELECT queries are allowed";
                return false;
            }

            // Block dangerous keywords
            string[] blocked =
            {
                "insert", "update", "delete", "truncate",
                "drop", "alter", "create",
                "merge", "call"
            };

            if (blocked.Any(k => normalized.Contains(k)))
            {
                error = "Query contains restricted operations";
                return false;
            }

            // Block multiple statements
            if (normalized.Contains(";"))
            {
                error = "Multiple statements are not allowed";
                return false;
            }

            return true;
        }
    }
}
