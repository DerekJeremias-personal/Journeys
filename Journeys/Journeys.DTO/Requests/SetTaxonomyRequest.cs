using Backend.Dto.Structures.Lookup;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DTO.Requests
{
    public class SetTaxonomyRequestDto
    {
        public string TaxonomyId { get; set; }

        public string TaxonomyName { get; set; }
        public string TaxonomyDescription { get; set; }
        public string TaxonomyType { get; set; }

    }

    /// <summary>
    /// Request DTO for creating taxonomy-specific lookup records.
    /// </summary>
    public class CreateTaxonomyLookupRequestDto
    {
        public string LookupKey { get; set; } = string.Empty;
        public string TaxonomyId { get; set; } = string.Empty;
        public string TaxonomyType { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public Dictionary<string, string>? Metadata { get; set; }
    }

    /// <summary>
    /// Request DTO for creating model-specific lookup records.
    /// </summary>
    public class CreateModelLookupRequestDto
    {
        public string LookupKey { get; set; } = string.Empty;
        public string ModelId { get; set; } = string.Empty;
        public string ModelType { get; set; } = string.Empty;
        public Dictionary<string, string>? Metadata { get; set; }
    }

    /// <summary>
    /// Result wrapper for lookup batch operations.
    /// </summary>
    public class LookupResultDto
    {
        public bool Found { get; set; }
        public LookupDto? Lookup { get; set; }
        public string? ErrorMessage { get; set; }
    }

}
