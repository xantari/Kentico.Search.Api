using Kentico.Validation.Common.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSwag.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Kentico.Search.Api.Controllers
{
    [Route("api/[controller]")]
    [SwaggerResponse(HttpStatusCode.InternalServerError, typeof(ApiError))]
    [ApiController]
    public class BaseController<T> : ControllerBase
    {
        protected ILogger<T> Logger { get; }

        public BaseController(ILogger<T> logger) => Logger = logger;
    }
}
