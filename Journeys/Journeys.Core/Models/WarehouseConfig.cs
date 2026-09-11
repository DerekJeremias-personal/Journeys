using Journeys.Core.Interfaces.Entities;
using System;
using System.Text.Json.Serialization;

namespace Journeys.Core.Models
{
    public class WarehouseConfig : TenantedModelBase
    {
        [JsonConstructor]
        public WarehouseConfig(
            string serviceprincipalname,
            string keyvaultsecretappid,
            string keyvaultsecretpassword,
            string schemafullname,
            string workspacefolder,
            string tenantId,
            string id,
            DateTimeOffset? createDate = null,
            DateTimeOffset? lastUpdated = null
        ) : base(tenantId, id)
        {
            ServicePrincipalName = serviceprincipalname;
            KeyVaultSecretAppId = keyvaultsecretappid;
            KeyVaultSecretPassword = keyvaultsecretpassword;
            SchemaFullName = schemafullname;
            WorkSpaceFolder = workspacefolder;
        }

        public string ServicePrincipalName { get; set; }
        public string KeyVaultSecretAppId { get; set; }
        public string KeyVaultSecretPassword { get; set; }
        public string SchemaFullName { get; set; }
        public string WorkSpaceFolder { get; set; }
    }
}
