using System;
using System.Collections.Generic;
using System.Text;

namespace Kentico.Validation.Common.Core.Models
{
    public class ApiError
    {   
        /// <summary>
        /// The Trace Id / Request Id of the request (this is useful for the caller because we could display this in a message to an end user for lookup 
        /// on our backend to see entire response flow leading up to the error)
        /// Serilog calls this the RequestId, which is actually the HttpContext.TraceIdentifier
        /// </summary>
        public string RequestId { get; set; }
        public string Message { get; set; }
        public ApiError(string message, string requestId)
        {
            Message = message;
            RequestId = requestId;
        }
    }
}
