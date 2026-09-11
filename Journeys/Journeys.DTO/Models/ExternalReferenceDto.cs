using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DTO.Models
{
    public class ExternalReferenceDto : DtoModelBase
    {
        public string Type { get; set; }

        public string Status { get; set; }

        public string MapFromId { get; set; }

        public string MapToId { get; set; }

    }
}
