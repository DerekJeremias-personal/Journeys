using Journeys.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.Interfaces.Entities
{
    public interface IReferenceable
    {
        string ExternalId { get; }
        string ExternalIdType { get; }
    }
}
