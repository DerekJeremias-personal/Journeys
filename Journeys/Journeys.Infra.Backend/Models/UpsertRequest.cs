using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Journeys.Infra.Backend.Models
{
    public class UpsertRequest<T>
    {
        public string ModelId { get; set; }

        public string ModelType { get; set; }

        public T Entity { get; set; }

        public UpsertRequest(string modelType, string modelId, T entity)
        {
            ModelId = modelId;
            ModelType = modelType;
            Entity = entity;
        }

    }

    public class KeyValueStorageMoveRequest<T> : UpsertRequest<T>
    {
        public Dictionary<string, string> NewPartition { get; set; }

        public KeyValueStorageMoveRequest(string modelType, string modelId, T entity, Dictionary<string, string> newPartition)
            : base(modelType, modelId, entity) 
        {
            NewPartition = newPartition;
        }

    }

}
