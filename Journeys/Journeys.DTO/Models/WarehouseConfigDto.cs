using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DTO.Models
{
    public class WarehouseConfigDto : DtoModelBase
    {
        public string ServicePrincipalName { get; set; }
        public string KeyVaultSecretAppId { get; set; }
        public string KeyVaultSecretPassword { get; set; }
        public string SchemaFullName { get; set; }
        public string WorkSpaceFolder { get; set; }

    }
}
