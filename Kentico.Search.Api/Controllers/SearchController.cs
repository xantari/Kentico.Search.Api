using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Kentico.Search.Api.Models.RequestModels;
using Kentico.Search.Api.Models.ResponseModels;
using Kentico.Search.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSwag.Annotations;

namespace Kentico.Search.Api.Controllers
{
    /// <summary>
    /// Azure Search API Services
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class SearchController : BaseController<SearchController>
    {
        private readonly ISearchService _service;
        public SearchController(ILogger<SearchController> logger, ISearchService service) : base(logger) => _service = service;

        /// <summary>
        /// Search by Keyword / Terms
        /// </summary>
        /// <param name="model">Search By Terms Request Model</param>
        /// <returns></returns>
        [HttpGet(nameof(SearchByTerms))]
        [SwaggerResponse(HttpStatusCode.OK, typeof(SearchByTermsResponseModel))]
        public async Task<SearchByTermsResponseModel> SearchByTerms([FromQuery] SearchByTermsRequestModel model) =>
            await _service.GetSearchResultsAsync(model);

        /// <summary>
        /// Rebuild Search Indexes
        /// </summary>
        /// <param name="searchServiceName">Driven by AzureSearchConfig.Name in the sites configuration file. Possible values: "yourkenticosite.org Search"</param>
        /// <returns></returns>
        [HttpPost(nameof(RebuildIndexes))]
        [SwaggerResponse(HttpStatusCode.OK, typeof(RebuildIndexResponseModel))]
        public async Task<RebuildIndexResponseModel> RebuildIndexes(string searchServiceName) => await _service.RebuildIndexesAsync(searchServiceName);

        /// <summary>
        /// Populate an individual index. If the index already has items in it then a merge upload will occur.
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost(nameof(PopulateIndex))]
        [SwaggerResponse(HttpStatusCode.OK, typeof(PopulateIndexResponseModel))]
        public async Task<PopulateIndexResponseModel> PopulateIndex(PopulateIndexRequestModel model) => await _service.PopulateIndexAsync(model);

        /// <summary>
        /// Repopulates all indexes
        /// </summary>
        /// <param name="searchServiceName">Driven by AzureSearchConfig.Name in the sites configuration file. Possible values: "yourkenticosite.org Search"</param>
        /// <returns></returns>
        [HttpPost(nameof(PopulateAllIndexes))]
        [SwaggerResponse(HttpStatusCode.OK, typeof(PopulateAllIndexResponseModel))]
        public async Task<PopulateAllIndexResponseModel> PopulateAllIndexes(string searchServiceName) => await _service.PopulateAllIndexesAsync(searchServiceName);

        /// <summary>
        /// Retrieves all search definitions and the registered indexes in each search definition
        /// </summary>
        /// <returns></returns>
        [HttpGet(nameof(GetAllSearchDefinitions))]
        [SwaggerResponse(HttpStatusCode.OK, typeof(AllSearchServiceResponseModel))]
        public async Task<AllSearchServiceResponseModel> GetAllSearchDefinitions() => await _service.GetAllSearchDefinitions();
    }
}