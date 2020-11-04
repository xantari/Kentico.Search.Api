using Kentico.Search.Api.Controllers;
using Kentico.Search.Api.Models.RequestModels;
using Kentico.Search.Api.Services;
using Kentico.Search.Api.Services.Factories;
using Azure.Storage.Blobs;
using Kentico.Kontent.Delivery.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Search;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Kentico.Search.Api.Tests
{
    public class SearchServiceControllerTests : IClassFixture<CustomWebApplicationFactory<Startup>>
    {
        private Mock<ILogger<SearchController>> _controllerLogger;
        private Mock<ILogger<SearchService>> _serviceLogger;

        //private ISearchService _service;
        private Mock<IHttpClientFactory> _httpFactory;
        private Mock<IKenticoManagementClientFactory> _managementClientFactory;
        private Mock<IAzureClientFactory<BlobServiceClient>> _blobClientFactory;
        private Mock<ISearchServiceClientFactory> _searchClientFactory;
        private Mock<IDeliveryClientFactory> _clientFactory;
        private Mock<IDeliveryCacheManager> _cacheManager;
        private Mock<ISearchServiceClient> _searchServiceClient;

        private readonly CustomWebApplicationFactory<Startup> _factory;

        public SearchServiceControllerTests(CustomWebApplicationFactory<Startup> factory)
        {
            _controllerLogger = new Mock<ILogger<SearchController>>();
            _serviceLogger = new Mock<ILogger<SearchService>>();
            _httpFactory = new Mock<IHttpClientFactory>();
            _managementClientFactory = new Mock<IKenticoManagementClientFactory>();
            _blobClientFactory = new Mock<IAzureClientFactory<BlobServiceClient>>();
            _searchClientFactory = new Mock<ISearchServiceClientFactory>();
            _clientFactory = new Mock<IDeliveryClientFactory>();
            _cacheManager = new Mock<IDeliveryCacheManager>();
            _searchServiceClient = new Mock<ISearchServiceClient>();

            _factory = factory;
        }

        [Fact]
        public async Task GetAllSearchDefinitions_Test()
        {
            //This one tests with the actual loadding of the main web applications settings file.
            //We can use the actual startup of the application for this as this just echo's back the configuration file (appsettings.json) data
            var scope = new CustomWebApplicationFactory<Startup>().Server.Host.Services.CreateScope();
            var searchService = scope.ServiceProvider.GetRequiredService<ISearchService>();

            //Arrange
            SearchController controller = new SearchController(_controllerLogger.Object, searchService);

            //Act
            var result = await controller.GetAllSearchDefinitions();

            //Assert
            Assert.NotNull(result);
        }
    }
}
