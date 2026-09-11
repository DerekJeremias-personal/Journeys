using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Journeys.DTO.Exceptions
{
    public class APIErrorsException : Exception
    {
        public Dictionary<string, string> Errors { get; set; } = new Dictionary<string, string>();
        public APIErrorsException() : base() { }
        public APIErrorsException(Dictionary<string, string> errors) : base(errors == null ? "APIErrorsException thrown but errors was null" : JsonSerializer.Serialize(errors))
        {
            Errors = errors;
        }
        public string AccountId { get; set; }
    }
}
