using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Infra.Backend.Models
{
    public class GetManyRequest
    {
        public string ModelId { get; set; }
        public string ModelType { get; set; }

        public List<string>? ObjectIds { get; set; }

        public List<ObjectIdWithPK>? ObjectIdsWithPKs { get; set; }
    }

    public class ObjectIdWithPK
    {
        public ObjectIdWithPK(string id, Dictionary<string, string> pKs)
        {
            Id = id;
            PKs = pKs;
        }

        public string Id { get; set; }
        public Dictionary<string, string> PKs { get; set; }
    }

}
