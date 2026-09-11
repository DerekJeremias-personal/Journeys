using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DTO.Models
{
    public class DtoModelBase
    {
        public DtoModelBase() { } // Parameterless constructor

        public string? TenantId { get; set; }
        public string? Id { get; set; }
        public string? Etag { get; set; }
    }
}
