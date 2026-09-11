using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Infra.Backend.Models
{
    /// <summary>
    /// Represents a field validation error response from the backend
    /// </summary>
    public class FieldValidationErrorResponse
    {
        public string Type { get; set; }
        public string Code { get; set; }
        public string Message { get; set; }
        public Dictionary<string, string> ValidationErrors { get; set; }
    }

    /// <summary>
    /// Represents a general error response from the backend
    /// </summary>
    public class ErrorResponse
    {
        public string Code { get; set; }
        public string Message { get; set; }
        public string RawMessage { get; set; }
        public string StackTrace { get; set; }
        public string Type { get; set; }
    }
}
