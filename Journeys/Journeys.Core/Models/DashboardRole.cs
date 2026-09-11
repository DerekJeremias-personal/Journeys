using Journeys.Core.Interfaces.Entities;
using System;
using System.Text.Json.Serialization;

namespace Journeys.Core.Models
{
    public class DashboardRole : TenantedModelBase, IReferenceable
    {
        [JsonConstructor]
        public DashboardRole(
            string roleId,
            string dashboardId,
            string tenantId,
            string id,
            DateTimeOffset? createDate = null,
            DateTimeOffset? lastUpdated = null
        ) : base(tenantId, id)
        {
            RoleId = roleId;
            DashboardId = dashboardId;
            CreateDate = createDate ?? DateTimeOffset.UtcNow;
            LastUpdated = lastUpdated ?? DateTimeOffset.UtcNow;
        }

        /// <summary>
        /// In support of IReferenceable
        /// </summary>
        public string ExternalId => Id;

        /// <summary>
        /// In support of IReferenceable
        /// </summary>
        public string ExternalIdType => "DashboardRole";

        public string RoleId { get; set; }

        public string DashboardId { get; set; }

        public DateTimeOffset? CreateDate { get; set; }

        public DateTimeOffset? LastUpdated { get; set; }
    }
}
