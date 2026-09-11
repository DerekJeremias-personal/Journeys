using Journeys.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.Interfaces.Entities
{
    public interface IIdempotent<T>
    {
        bool IsNewer(T storedEntity, T newEntity);
    }
}
