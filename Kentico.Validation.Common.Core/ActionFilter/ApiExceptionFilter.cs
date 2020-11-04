using Kentico.Validation.Common.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using System.Net;

namespace Kentico.Validation.Common.Core.ActionFilter
{
    public class ApiExceptionFilter : IExceptionFilter
    {
        private readonly ILogger<ApiExceptionFilter> _logger;
        public ApiExceptionFilter(ILogger<ApiExceptionFilter> logger)
        {
            _logger = logger;
        }

        public void OnException(ExceptionContext context)
        {
            _logger.LogError(context.Exception, "Unhandled API Exception occurred. Query String: {querystring}",
                context.HttpContext.Request.QueryString);
            var error = new ApiError(context.Exception.Message, context.HttpContext.TraceIdentifier);
            context.Result = new ObjectResult(error);
            context.ExceptionHandled = true;
            context.HttpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        }
    }
}
