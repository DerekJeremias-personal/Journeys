using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DTO.Models
{
    public class DashboardRoleDto : DtoModelBase
    {
        public string RoleId { get; set; }

        public string DashboardId { get; set; }

        public DateTimeOffset CreateDate { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;

    }
}
