using Journeys.DTO.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Journeys.Core.Interfaces.Services
{
    public interface IWarehouseAuthService
    {
        Task<string> GetToken(WarehouseConfigDto config);
        Task<string> RequestDashboardTokenAsync(Dictionary<string, string> formData, string basicAuthHeader);
        Task<JsonElement> GetDashboardTokenInfoAsync(string dashboardId, string viewerId, string externalValue, string oidcToken);
    }
}
