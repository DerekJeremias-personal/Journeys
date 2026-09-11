using Backend.Dto.Requests;
using Backend.Dto.Structures.Tenant;
using Journeys.DTO.Responses;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Journeys.Core.Interfaces.DataStorage
{
    /// <summary>
    /// HTTP adapter for global <c>api/Tenant</c> registry endpoints (not scoped under <c>api/{tenantId}/...</c>).
    /// </summary>
    public interface ITenantDataAdapter
    {
        /// <summary>GET <c>api/Tenant/get/{id}</c>. Returns null if the tenant is not found (404).</summary>
        Task<TenantDto?> GetTenantAsync(
            string id,
            bool includeDeleted = false,
            CancellationToken cancellationToken = default,
            JsonSerializerOptions serializerOptions = null);

        /// <summary>GET <c>api/Tenant/getbyname/{name}</c>. Returns null if not found (404).</summary>
        Task<TenantDto?> GetTenantByNameAsync(
            string name,
            bool includeDeleted = false,
            CancellationToken cancellationToken = default,
            JsonSerializerOptions serializerOptions = null);

        /// <summary>POST <c>api/Tenant/getmany</c>.</summary>
        Task<ContinuableList<TenantDto>> GetManyTenantsAsync(
            GetManyTenantsRequest request,
            CancellationToken cancellationToken = default,
            JsonSerializerOptions serializerOptions = null);

        /// <summary>POST <c>api/Tenant/all</c>.</summary>
        Task<ContinuableList<TenantDto>> GetAllTenantsAsync(
            GetAllTenantsRequest request,
            CancellationToken cancellationToken = default,
            JsonSerializerOptions serializerOptions = null);

        /// <summary>POST <c>api/Tenant/save</c>.</summary>
        Task<TenantDto> SaveTenantAsync(
            TenantDto tenantDto,
            CancellationToken cancellationToken = default,
            JsonSerializerOptions serializerOptions = null);

        /// <summary>POST <c>api/Tenant/delete/{id}</c> (soft delete). Returns null if not found (404).</summary>
        Task<TenantDto?> SoftDeleteTenantAsync(
            string id,
            CancellationToken cancellationToken = default,
            JsonSerializerOptions serializerOptions = null);
    }
}
