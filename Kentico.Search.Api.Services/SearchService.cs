using Kentico.Common.Extensions;
using Kentico.Search.Api.Models;
using Kentico.Search.Api.Models.Enums;
using Kentico.Search.Api.Models.RequestModels;
using Kentico.Search.Api.Models.ResponseModels;
using Kentico.Search.Api.Models.Settings;
using Kentico.Search.Api.Services.Factories;
using Kentico.Web.Models;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Kentico.Kontent.Delivery;
using Kentico.Kontent.Delivery.Abstractions;
using Kentico.Kontent.Management;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using NeoSmart.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using ContentType = Kentico.Search.Api.Models.Enums.ContentType;
using System.Globalization;
using AngleSharp.Html;
using Kentico.Kontent.Delivery.Urls.QueryParameters;
using Kentico.Kontent.Delivery.Urls.QueryParameters.Filters;
using Microsoft.Extensions.Options;

namespace Kentico.Search.Api.Services
{
    public interface ISearchService
    {
        Task<SearchByTermsResponseModel> GetSearchResultsAsync(SearchByTermsRequestModel requestModel);

        Task<RebuildIndexResponseModel> RebuildIndexesAsync(string searchServiceName);

        Task<PopulateIndexResponseModel> PopulateIndexAsync(PopulateIndexRequestModel requestModel);

        Task<PopulateAllIndexResponseModel> PopulateAllIndexesAsync(string searchServiceName);

        Task<AllSearchServiceResponseModel> GetAllSearchDefinitions();
    }

    public class SearchService : ISearchService
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly IKenticoManagementClientFactory _managementClientFactory;
        private readonly IAzureClientFactory<BlobServiceClient> _blobClientFactory;
        private readonly ISearchServiceClientFactory _searchClientFactory;
        private readonly ILogger<SearchService> _logger;
        private readonly IDeliveryClientFactory _clientFactory;
        private readonly ProjectOptions _projectOptions;

        public SearchService(IDeliveryClientFactory clientFactory, IHttpClientFactory httpFactory, IKenticoManagementClientFactory managementClientFactory, IAzureClientFactory<BlobServiceClient> blobClientFactory,
            ISearchServiceClientFactory searchServiceClientFactory, ILogger<SearchService> logger, IOptions<ProjectOptions> projectOptions)
        {
            _httpFactory = httpFactory;
            _managementClientFactory = managementClientFactory;
            _blobClientFactory = blobClientFactory;
            _searchClientFactory = searchServiceClientFactory;
            _logger = logger;
            _clientFactory = clientFactory;
            _projectOptions = projectOptions.Value;
        }

        public async Task<SearchByTermsResponseModel> GetSearchResultsAsync(SearchByTermsRequestModel requestModel)
        {
            var model = new SearchByTermsResponseModel();

            model.SearchTerms = requestModel.SearchTerms;

            var searchConfig = _projectOptions.AzureSearchConfigs.Where(p => p.Name == requestModel.SearchServiceName).FirstOrDefault();

            if (searchConfig == null)
                throw new Exception($"Search service {requestModel.SearchServiceName} definition not found.");

            foreach (var index in searchConfig.IndexDefinitions)
            {
                if (index.IndexType == IndexType.KenticoKontent)
                {
                    var searchResults = await GetIndexSearchResults(requestModel, index);
                    model.TotalResults += searchResults.TotalResults;
                    model.Results.Add(searchResults);
                }
                if (index.IndexType == IndexType.AzureBlobStorage)
                {
                    var searchResults = await GetBlobIndexSearchResults(requestModel, index);
                    model.TotalResults += searchResults.TotalResults;
                    model.Results.Add(searchResults);
                }
            }

            //model.PageNumber = pageNumber;
            //model.ResultsPerPage = 10;
            //Calculate the page count
            //model.TotalPages = model.TotalResults / model.ResultsPerPage;

            return model;
        }

        public async Task<RebuildIndexResponseModel> RebuildIndexesAsync(string searchServiceName)
        {
            var model = new RebuildIndexResponseModel();
            try
            {
                // Get the search client
                var searchConfig = _projectOptions.AzureSearchConfigs.Where(p => p.Name == searchServiceName).FirstOrDefault();

                if (searchConfig == null)
                    throw new Exception($"Search service {searchServiceName} definition not found.");

                var serviceClient = _searchClientFactory.Get(searchConfig.Name);

                foreach (var config in searchConfig.IndexDefinitions)
                {
                    if (config.ContentType == ContentType.KenticoPageContent)
                    {
                        if (await serviceClient.Indexes.ExistsAsync(config.IndexName)) //Example: "page-search-index"
                        {
                            await serviceClient.Indexes.DeleteAsync(config.IndexName);
                            await CreatePageIndex(serviceClient, config.IndexName);
                        }
                        else
                            await CreatePageIndex(serviceClient, config.IndexName);

                        model.Messages.Add($"Rebuilt index {config.IndexName}");
                    }

                    if (config.ContentType == ContentType.KenticoNewsContent)
                    {
                        if (await serviceClient.Indexes.ExistsAsync(config.IndexName)) //Example: "news-search-index"
                        {
                            await serviceClient.Indexes.DeleteAsync(config.IndexName);
                            await CreateNewsIndex(serviceClient, config.IndexName);
                        }
                        else
                            await CreateNewsIndex(serviceClient, config.IndexName);

                        model.Messages.Add($"Rebuilt index {config.IndexName}");
                    }

                    if (config.ContentType == ContentType.AzureBlob)
                    {
                        if (await serviceClient.Indexes.ExistsAsync(config.IndexName))
                        {
                            await serviceClient.Indexes.DeleteAsync(config.IndexName);
                            await CreateBlobIndex(serviceClient, config);
                        }
                        else
                            await CreateBlobIndex(serviceClient, config);

                        //Now build the data source
                        if (await serviceClient.DataSources.ExistsAsync(searchConfig.AzureBlobDatasourceName))
                        {
                            await serviceClient.DataSources.DeleteAsync(searchConfig.AzureBlobDatasourceName);
                            await CreateBlobDataSource(serviceClient, searchConfig, config.IndexName);
                        }
                        else
                            await CreateBlobDataSource(serviceClient, searchConfig, config.IndexName);

                        //Now link the search index and data source together with an indexer
                        if (await serviceClient.Indexers.ExistsAsync(searchConfig.AzureBlobIndexerName)) 
                        {
                            await serviceClient.Indexers.DeleteAsync(searchConfig.AzureBlobIndexerName);
                            await CreateBlobIndexer(serviceClient, searchConfig, config);
                        }
                        else
                            await CreateBlobIndexer(serviceClient, searchConfig, config);

                        model.Messages.Add($"Rebuilt index {config.IndexName}");
                    }
                }

                model.Success = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                model.Success = false;
                model.Messages.Add(ex.ToString());
            }

            return model;
        }

        public async Task<PopulateIndexResponseModel> PopulateIndexAsync(PopulateIndexRequestModel requestModel)
        {
            var model = new PopulateIndexResponseModel() { IndexName = requestModel.IndexName };

            try
            {
                // Get the search client
                var searchConfig = _projectOptions.AzureSearchConfigs.Where(p => p.Name == requestModel.SearchServiceName).FirstOrDefault();

                if (searchConfig == null)
                    throw new Exception($"Search service {requestModel.SearchServiceName} definition not found.");

                var serviceClient = _searchClientFactory.Get(searchConfig.Name);

                var config = searchConfig.IndexDefinitions.Where(p => p.IndexName.ToLower() == requestModel.IndexName.ToLower()).FirstOrDefault();

                if (config == null)
                    throw new Exception($"Index {requestModel.IndexName} definition not found.");

                if (config.ContentType == ContentType.KenticoPageContent)
                {
                    await PopulatePageIndex(searchConfig, config);

                    model.Messages.Add($"Populated page index {config.IndexName}");
                }

                if (config.ContentType == ContentType.KenticoNewsContent)
                {
                    await PopulateNewsIndex(searchConfig, config);

                    model.Messages.Add($"Populated news index {config.IndexName}");
                }

                if (config.ContentType == ContentType.AzureBlob)
                {
                    await PopulateAssetsBlobStorage(searchConfig, config);

                    model.Messages.Add($"Populated blob storage index {config.IndexName}");
                }

                model.Success = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                model.Success = false;
                model.Messages.Add(ex.ToString());
            }

            return model;
        }

        public async Task<PopulateAllIndexResponseModel> PopulateAllIndexesAsync(string searchServiceName)
        {
            var model = new PopulateAllIndexResponseModel();

            var searchConfig = _projectOptions.AzureSearchConfigs.Where(p => p.Name == searchServiceName).FirstOrDefault();

            if (searchConfig == null)
                throw new Exception($"Search service {searchServiceName} definition not found.");

            model.Success = true;

            foreach (var index in searchConfig.IndexDefinitions)
            {
                var requestModel = new PopulateIndexRequestModel() { SearchServiceName = searchServiceName, IndexName = index.IndexName };
                var result = await PopulateIndexAsync(requestModel);
                if (!result.Success)
                {
                    model.Messages.Add($"Failure on index: {index.IndexName}");
                    model.Messages.AddRange(result.Messages);
                    model.Success = false;
                }
                else
                {
                    model.Messages.AddRange(result.Messages);
                }
            }

            return model;
        }

        public async Task<AllSearchServiceResponseModel> GetAllSearchDefinitions()
        {
            var model = new AllSearchServiceResponseModel();

            foreach (var searchConfig in _projectOptions.AzureSearchConfigs)
            {
                var config = new SearchServiceResponseModel()
                {
                    Name = searchConfig.Name
                };
                foreach (var index in searchConfig.IndexDefinitions)
                {
                    config.Indexes.Add(new SearchIndexDefinitionResponseModel()
                    {
                        ContentType = index.ContentType,
                        IndexFriendlyName = index.IndexFriendlyName,
                        IndexName = index.IndexName,
                        IndexType = index.IndexType
                    });
                }

                model.SearchServices.Add(config);
            }

            return await Task.FromResult<AllSearchServiceResponseModel>(model);
        }

        private async Task<SearchResultResponseModel> GetIndexSearchResults(SearchByTermsRequestModel requestModel, SearchIndexDefinition indexDefinition)
        {
            var model = new SearchResultResponseModel();
            var searchService = _searchClientFactory.Get(requestModel.SearchServiceName);
            var indexClient = searchService.Indexes.GetClient(indexDefinition.IndexName);

            var searchParameters = new SearchParameters();
            searchParameters.HighlightPreTag = $"<strong class=\"{requestModel.HighlightCssClass}\">";
            searchParameters.HighlightPostTag = "</strong>";
            searchParameters.HighlightFields = new List<string>() { indexDefinition.ContentField };
            searchParameters.IncludeTotalResultCount = true;
            var results = await indexClient.Documents.SearchAsync(requestModel.SearchTerms, searchParameters);

            model.SearchTerms = requestModel.SearchTerms;
            model.TotalResults += results.Results.Count();
            model.IndexName = indexDefinition.IndexName;
            model.IndexFriendlyName = indexDefinition.IndexFriendlyName;

            foreach (var doc in results.Results)
            {
                var result = new SearchResultItemResponseModel()
                {
                    Score = doc.Score,
                    Url = doc.Document[indexDefinition.UrlField].ToString(),
                    Content = doc.Document[indexDefinition.ContentField].ToString(),
                    Title = doc.Document[indexDefinition.TitleField].ToString(),
                    IsBlob = false
                };

                //Get any highlights for keyword highlighting
                if (doc.Highlights != null)
                {
                    foreach (var highlight in doc.Highlights)
                    {
                        foreach (var hitOnTerm in highlight.Value)
                        {
                            result.Highlights.Add(hitOnTerm);
                        }
                    }
                }

                model.Results.Add(result);
            }
            return model;
        }

        private async Task<SearchResultResponseModel> GetBlobIndexSearchResults(SearchByTermsRequestModel requestModel, SearchIndexDefinition indexDefinition)
        {
            var model = new SearchResultResponseModel();
            var indexClient = _searchClientFactory.Get(requestModel.SearchServiceName).Indexes.GetClient(indexDefinition.IndexName);

            var searchConfig = _projectOptions.AzureSearchConfigs.Where(p => p.Name == requestModel.SearchServiceName).FirstOrDefault();

            var searchParameters = new SearchParameters();
            searchParameters.HighlightPreTag = $"<strong class=\"{requestModel.HighlightCssClass}\">";
            searchParameters.HighlightPostTag = "</strong>";
            searchParameters.HighlightFields = new List<string>() { indexDefinition.ContentField };
            searchParameters.IncludeTotalResultCount = true;
            var results = await indexClient.Documents.SearchAsync(requestModel.SearchTerms, searchParameters);

            model.SearchTerms = requestModel.SearchTerms;
            model.TotalResults += results.Results.Count();
            model.IndexName = indexDefinition.IndexName;
            model.IndexFriendlyName = indexDefinition.IndexFriendlyName;

            foreach (var doc in results.Results)
            {
                //Leaving this note in here incase we need this info in the future as it took awhile to figure out
                //Azure Base64 strings are not normal base64 strings, they appear to be RFC4648 base64 strings.
                //The number at the end denotes how many padding characters (=) are supposed to be present
                //https://stackoverflow.com/questions/44338134/how-to-decode-metadata-storage-path-produced-by-azure-search-indexer-in-net-cor
                //var stringToDecode = doc.Document["metadata_storage_path"].ToString();
                //var blobUrl = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(stringToDecode.Substring(0, stringToDecode.Length - 1)));

                var url = doc.Document[indexDefinition.UrlField].ToString();
                var kenticoCdnUrl = new Uri(url);
                var ourAssetUrl = $"{searchConfig.WebsiteHomeUrl}/{searchConfig.AssetsControllerName}{kenticoCdnUrl.PathAndQuery}";

                string title = string.Empty;
                //Order of searching for title. If the Title on the document is defined use that. If that is not defined, use the Description field as the title (it's a single line in Kentico)
                //and if thats not defined, then fall back to the file name
                if (doc.Document[indexDefinition.TitleField] != null && !string.IsNullOrEmpty(doc.Document[indexDefinition.TitleField].ToString()))
                    title = doc.Document[indexDefinition.TitleField].ToString();
                if (string.IsNullOrEmpty(title) && (doc.Document[indexDefinition.DescriptionField] != null && !string.IsNullOrEmpty(doc.Document[indexDefinition.DescriptionField].ToString())))
                    title = doc.Document[indexDefinition.DescriptionField].ToString();
                if (string.IsNullOrEmpty(title)) //We still haven't found the filled out info in title or description metadata on this asset in Kentico, fallback to returning just the filename (will always be filled in)
                    title = doc.Document[indexDefinition.FileNameField].ToString();

                var result = new SearchResultItemResponseModel()
                {
                    Score = doc.Score,
                    Url = url,
                    Content = doc.Document[indexDefinition.ContentField].ToString(),
                    Title = title,
                    IsBlob = true
                };

                //Get any highlights for keyword highlighting
                if (doc.Highlights != null)
                {
                    foreach (var highlight in doc.Highlights)
                    {
                        foreach (var hitOnTerm in highlight.Value)
                        {
                            result.Highlights.Add(hitOnTerm);
                        }
                    }
                }

                model.Results.Add(result);
            }
            return model;
        }

        private async Task CreatePageIndex(ISearchServiceClient serviceClient, string indexName)
        {
            _logger.LogInformation($"Creating Page Index: {indexName}");
            // Create the index definition
            var definition = new Microsoft.Azure.Search.Models.Index()
            {
                Name = indexName,
                Fields = new[]
                {
                    new Field ( "CodeName", DataType.String) { IsKey = true,  IsSearchable = false, IsFilterable = false, IsSortable = true, IsFacetable = false, IsRetrievable = true},
                    new Field ( "Type", DataType.String) { IsKey = false,  IsSearchable = false, IsFilterable = true, IsSortable = false, IsFacetable = true, IsRetrievable = true},
                    new Field ( "Content", DataType.String) { IsKey = false,  IsSearchable = true, IsFilterable = true, IsSortable = true, IsFacetable = true, IsRetrievable = true, Analyzer = "standard.lucene"},
                    new Field ( "Url", DataType.String) { IsKey = false,  IsSearchable = true, IsFilterable = true, IsSortable = true, IsFacetable = true, IsRetrievable = true},
                    new Field ( "Title", DataType.String) { IsKey = false,  IsSearchable = true, IsFilterable = true, IsSortable = true, IsFacetable = true, IsRetrievable = true, Analyzer = "standard.lucene"},
                    new Field ( "UrlSlug", DataType.String) { IsKey = false,  IsSearchable = false, IsFilterable = false, IsSortable = false, IsFacetable = false, IsRetrievable = true},
                    new Field ( "Date", DataType.DateTimeOffset) { IsKey = false, IsSearchable = false,  IsFilterable = true, IsSortable = true,  IsFacetable = true, IsRetrievable = true}
                    },
            };
            // Create the index
            await serviceClient.Indexes.CreateAsync(definition);
        }

        private async Task CreateNewsIndex(ISearchServiceClient serviceClient, string indexName)
        {
            _logger.LogInformation($"Creating News Index: {indexName}");
            // Create the index definition
            var definition = new Microsoft.Azure.Search.Models.Index()
            {
                Name = indexName,
                Fields = new[]
                {
                    new Field ( "CodeName", DataType.String) { IsKey = true,  IsSearchable = false, IsFilterable = false, IsSortable = true, IsFacetable = false, IsRetrievable = true},
                    new Field ( "Type", DataType.String) { IsKey = false,  IsSearchable = false, IsFilterable = true, IsSortable = false, IsFacetable = true, IsRetrievable = true},
                    new Field ( "Content", DataType.String) { IsKey = false,  IsSearchable = true, IsFilterable = true, IsSortable = true, IsFacetable = true, IsRetrievable = true, Analyzer = "standard.lucene"},
                    new Field ( "Synopsis", DataType.String) { IsKey = false,  IsSearchable = true, IsFilterable = true, IsSortable = true, IsFacetable = true, IsRetrievable = true, Analyzer = "standard.lucene"},
                    new Field ( "Url", DataType.String) { IsKey = false,  IsSearchable = true, IsFilterable = true, IsSortable = true, IsFacetable = true, IsRetrievable = true},
                    new Field ( "UrlSlug", DataType.String) { IsKey = false,  IsSearchable = false, IsFilterable = false, IsSortable = false, IsFacetable = false, IsRetrievable = true},
                    new Field ( "Title", DataType.String) { IsKey = false,  IsSearchable = true, IsFilterable = true, IsSortable = true, IsFacetable = true, IsRetrievable = true, Analyzer = "standard.lucene"},
                    //new Field ( "Location", DataType.String) { IsKey = false, IsSearchable = true,  IsFilterable = true, IsSortable = true,  IsFacetable = true, IsRetrievable = true},
                    new Field ( "Date", DataType.DateTimeOffset) { IsKey = false, IsSearchable = false,  IsFilterable = true, IsSortable = true,  IsFacetable = true, IsRetrievable = true}
                    },
            };
            // Create the index
            await serviceClient.Indexes.CreateAsync(definition);
        }

        private async Task CreateBlobIndex(ISearchServiceClient serviceClient, SearchIndexDefinition indexDefinition)
        {
            _logger.LogInformation($"Creating Blob Index: {indexDefinition.IndexName}");
            // Create the index definition
            //https://docs.microsoft.com/en-us/azure/search/search-howto-indexing-azure-blob-storage
            var definition = new Microsoft.Azure.Search.Models.Index()
            {
                Name = indexDefinition.IndexName,
                Fields = new[]
                {
                    new Field ( "content", DataType.String) { IsKey = false,  IsSearchable = true, IsFilterable = false, IsSortable = false, IsFacetable = false, IsRetrievable = true, Analyzer = "standard.lucene"},
                    new Field ( "metadata_storage_content_type", DataType.String) { IsKey = false,  IsSearchable = false, IsFilterable = false, IsSortable = false, IsFacetable = false, IsRetrievable = false},
                    new Field ( "metadata_storage_size", DataType.Int64) { IsKey = false,  IsSearchable = false, IsFilterable = false, IsSortable = false, IsFacetable = false, IsRetrievable = false},
                    new Field ( "metadata_storage_last_modified", DataType.DateTimeOffset) { IsKey = false,  IsSearchable = false, IsFilterable = false, IsSortable = false, IsFacetable = false, IsRetrievable = false},
                    new Field ( "metadata_storage_content_md5", DataType.String) { IsKey = false,  IsSearchable = false, IsFilterable = false, IsSortable = false, IsFacetable = false, IsRetrievable = false},
                    new Field ( "metadata_storage_name", DataType.String) { IsKey = false,  IsSearchable = false, IsFilterable = false, IsSortable = false, IsFacetable = false, IsRetrievable = false},
                    new Field ( "metadata_storage_path", DataType.String) { IsKey = true,  IsSearchable = false, IsFilterable = false, IsSortable = false, IsFacetable = false, IsRetrievable = true},
                    new Field ( "metadata_content_type", DataType.String) { IsKey = false,  IsSearchable = false, IsFilterable = false, IsSortable = false, IsFacetable = false, IsRetrievable = false},
                    new Field ( "metadata_language", DataType.String) { IsKey = false,  IsSearchable = false, IsFilterable = false, IsSortable = false, IsFacetable = false, IsRetrievable = false},
                    new Field ( "metadata_author", DataType.String) { IsKey = false,  IsSearchable = false, IsFilterable = false, IsSortable = false, IsFacetable = false, IsRetrievable = false},
                    new Field ( "metadata_creation_date", DataType.DateTimeOffset) { IsKey = false,  IsSearchable = false, IsFilterable = false, IsSortable = false, IsFacetable = false, IsRetrievable = false},
                    new Field ( indexDefinition.TitleField, DataType.String) { IsKey = false,  IsSearchable = true, IsFilterable = false, IsSortable = false, IsFacetable = false, IsRetrievable = true, Analyzer = "standard.lucene"},
                    new Field ( indexDefinition.DescriptionField, DataType.String) { IsKey = false,  IsSearchable = true, IsFilterable = false, IsSortable = false, IsFacetable = false, IsRetrievable = true, Analyzer = "standard.lucene"},
                    new Field ( indexDefinition.FileNameField, DataType.String) { IsKey = false,  IsSearchable = false, IsFilterable = false, IsSortable = false, IsFacetable = false, IsRetrievable = true},
                    new Field ( indexDefinition.UrlField, DataType.String) { IsKey = false,  IsSearchable = false, IsFilterable = false, IsSortable = false, IsFacetable = false, IsRetrievable = true},
                    },
            };
            // Create the index
            await serviceClient.Indexes.CreateAsync(definition);
        }

        private async Task<DataSource> CreateBlobDataSource(ISearchServiceClient serviceClient, AzureSearchConfig searchConfig, string indexName)
        {
            _logger.LogInformation($"Creating Blob Datasource: {indexName}");
            var dataSource = new DataSource();
            dataSource.Description = "Kentico Azure Blob Storage Datasource";
            dataSource.Type = DataSourceType.AzureBlob;
            dataSource.Name = searchConfig.AzureBlobDatasourceName;
            dataSource.Credentials = new DataSourceCredentials(searchConfig.AzureBlobStorageConnectionString);
            dataSource.Container = new DataContainer();
            dataSource.Container.Name = searchConfig.AzureBlobContainerName;
            //NativeBlobSoftDeleteDeletionDetectionPolicy is apparently still in preview mode and not in the release versino of the SDK
            //https://github.com/Azure/azure-sdk-for-net/issues/11435
            var policy = new SoftDeleteColumnDeletionDetectionPolicy(); 
            policy.SoftDeleteColumnName = searchConfig.SoftDeletedFieldName;
            policy.SoftDeleteMarkerValue = "true";
            dataSource.DataDeletionDetectionPolicy = policy;
            return await serviceClient.DataSources.CreateAsync(dataSource);
        }

        private async Task<Indexer> CreateBlobIndexer(ISearchServiceClient serviceClient, AzureSearchConfig searchConfig, SearchIndexDefinition indexDefinition)
        {
            _logger.LogInformation($"Creating Blob Indexer: {indexDefinition.IndexName}");
            var indexer = new Indexer();
            indexer.Description = "Kentico Azure Blob Storage Indexer";
            indexer.Name = searchConfig.AzureBlobIndexerName;
            indexer.DataSourceName = searchConfig.AzureBlobDatasourceName;
            indexer.TargetIndexName = indexDefinition.IndexName;
            indexer.Schedule = new IndexingSchedule(searchConfig.BlobIndexerRunInterval);
            indexer.Parameters = new IndexingParameters();
            indexer.Parameters.SetBlobExtractionMode(BlobExtractionMode.ContentAndMetadata);
            indexer.Parameters.MaxFailedItems = 0;
            indexer.Parameters.MaxFailedItemsPerBatch = 0;
            indexer.FieldMappings = new List<FieldMapping>();
            indexer.FieldMappings.Add(new FieldMapping() { 
                MappingFunction = FieldMappingFunction.Base64Encode(), 
                SourceFieldName = "metadata_storage_path", 
                TargetFieldName = "metadata_storage_path" });
            indexer.FieldMappings.Add(new FieldMapping()
            {
                //https://docs.microsoft.com/en-us/azure/search/search-indexer-field-mappings#base64decode
                //https://feedback.azure.com/forums/263029-azure-search/suggestions/20212159-base64decode-method-should-handle-standard-strings
                //The Base64Decode(false) is so that it can decrypt standard base64 strings
                MappingFunction = FieldMappingFunction.Base64Decode(false),
                SourceFieldName = "KenticoTitleBase64",
                TargetFieldName = indexDefinition.TitleField
            });
            indexer.FieldMappings.Add(new FieldMapping()
            {
                MappingFunction = FieldMappingFunction.Base64Decode(false),
                SourceFieldName = "KenticoDescriptionBase64_default", //We only support one language (the default) so far
                TargetFieldName = indexDefinition.DescriptionField
            });
            indexer.FieldMappings.Add(new FieldMapping()
            {
                MappingFunction = FieldMappingFunction.Base64Decode(false),
                SourceFieldName = "CDNUrlBase64", 
                TargetFieldName = indexDefinition.UrlField
            });
            indexer.FieldMappings.Add(new FieldMapping()
            {
                MappingFunction = FieldMappingFunction.Base64Decode(false),
                SourceFieldName = "FileNameBase64", 
                TargetFieldName = indexDefinition.FileNameField
            });
            return await serviceClient.Indexers.CreateAsync(indexer);
        }

        private async Task PopulatePageIndex(AzureSearchConfig searchConfig, SearchIndexDefinition indexDefinition)
        {
            _logger.LogInformation($"Begin Populating Page Index: {indexDefinition.IndexName}");
            ISearchServiceClient serviceClient = _searchClientFactory.Get(searchConfig.Name);
            List<IndexAction<Microsoft.Azure.Search.Models.Document>> lstActions = new List<IndexAction<Microsoft.Azure.Search.Models.Document>>();

            var deliveryClient = _clientFactory.Get(searchConfig.Name);

            // Get the content from Kentico Cloud
            var response = await deliveryClient.GetItemsAsync<object>(
                new InFilter("system.type", $"{LandingPage.Codename},{ContentPage.Codename}"),
                new DepthParameter(100),
                new LimitParameter(100),
                new OrderParameter("system.codename", SortOrder.Descending) //Required to ensure paging is deterministic
            );

            var httpClient = _httpFactory.CreateClient();

            List<string> pageCodenamesThatShouldExist = new List<string>();

            while (true)
            {
                foreach (var item in response.Items)
                {

                    string content = string.Empty;

                    switch (item)
                    {
                        case LandingPage _:
                            {
                                var contentModel = item as LandingPage;
                                _logger.LogInformation("Fetching {@page} content to send to Azure Search Service", contentModel.System);
                                try
                                {
                                    var rawContentUrl = indexDefinition.RawContentUrls.FirstOrDefault(p => p.Codename == LandingPage.Codename);
                                    var httpResponse = await httpClient.GetAsync(searchConfig.WebsiteHomeUrl + rawContentUrl.Url.Replace("{codename}", contentModel.System.Codename));
                                    var friendlyUrl = httpResponse.Headers.GetValues(Constants.FriendlyUrlResponseHeader).FirstOrDefault();
                                    content = await httpResponse.Content.ReadAsStringAsync();
                                    pageCodenamesThatShouldExist.Add(contentModel.System.Codename); //Unique kentico identifer for this content item
                                    var collapsedContent = HttpUtility.HtmlDecode(content.StripHtml(" ")).CondenseToWholeWords();
                                    var doc = new Microsoft.Azure.Search.Models.Document();
                                    doc.Add("Content", collapsedContent.Trim());
                                    doc.Add("CodeName", contentModel.System.Codename);
                                    doc.Add("Type", contentModel.System.Type);
                                    doc.Add("Title", contentModel.MetadataMetaTitle);
                                    doc.Add("Date", contentModel.System.LastModified);
                                    doc.Add("UrlSlug", contentModel.UrlSlug);
                                    doc.Add("Url", friendlyUrl);
                                    lstActions.Add(IndexAction.MergeOrUpload(doc));
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Error Fetching {@page} content to send to Azure Search Service. Page may not yet be available in the site cache.", contentModel.System);
                                }
   
                                break;
                            }
                        case ContentPage _:
                            {
                                var contentModel = item as ContentPage;
                                if (contentModel.IsRestricted)
                                {
                                    _logger.LogInformation("Skipping indexing of a restricted page... {page}", contentModel.System);
                                    continue;
                                }
                                _logger.LogInformation("Fetching {@page} content to send to Azure Search Service", contentModel.System);
                                try
                                {
                                    var rawContentUrl = indexDefinition.RawContentUrls.FirstOrDefault(p => p.Codename == ContentPage.Codename);
                                    var httpResponse = await httpClient.GetAsync(searchConfig.WebsiteHomeUrl + rawContentUrl.Url.Replace("{codename}", contentModel.System.Codename));
                                    var friendlyUrl = httpResponse.Headers.GetValues(Constants.FriendlyUrlResponseHeader).FirstOrDefault();
                                    content = await httpResponse.Content.ReadAsStringAsync();
                                    pageCodenamesThatShouldExist.Add(contentModel.System.Codename); //Unique kentico identifer for this content item
                                    var collapsedContent = HttpUtility.HtmlDecode(content.StripHtml(" ")).CondenseToWholeWords();
                                    var doc = new Microsoft.Azure.Search.Models.Document();
                                    doc.Add("Content", collapsedContent);
                                    doc.Add("CodeName", contentModel.System.Codename);
                                    doc.Add("Type", contentModel.System.Type);
                                    doc.Add("Title", contentModel.MetadataMetaTitle);
                                    doc.Add("Date", contentModel.System.LastModified);
                                    doc.Add("UrlSlug", contentModel.UrlSlug);
                                    doc.Add("Url", friendlyUrl);
                                    lstActions.Add(IndexAction.MergeOrUpload(doc));
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Error Fetching {@page} content to send to Azure Search Service. Page may not yet be available in the site cache.", contentModel.System);
                                }

                                break;
                            }
                    }
                }

                if (string.IsNullOrEmpty(response.Pagination.NextPageUrl)) //Any further pages of results to process?
                {
                    break;
                }

                response = await deliveryClient.GetItemsAsync<object>(
                        new InFilter("system.type", $"{LandingPage.Codename},{ContentPage.Codename}"),
                        new DepthParameter(100),
                        new LimitParameter(100),
                        new SkipParameter(response.Pagination.Skip + response.Pagination.Count),
                        new OrderParameter("system.codename", SortOrder.Descending) //Required to ensure paging is deterministic
                    );
            }

            //Now delete the index items where they no longer exist in kentico (deleted pages)
            ISearchIndexClient indexClient = serviceClient.Indexes.GetClient(indexDefinition.IndexName);

            await RemoveStaleIndexContent(searchConfig, indexDefinition, pageCodenamesThatShouldExist);

            if (lstActions.Count > 0)
            {
                var batch = IndexBatch.New(lstActions);
                try
                {
                    var indexResult = indexClient.Documents.Index(batch); //Request a reindexing request
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, ex.Message);
                    throw;
                }
            }

            _logger.LogInformation($"Ended Populating Page Index: {indexDefinition.IndexName}");
        }

        private async Task RemoveStaleIndexContent(AzureSearchConfig searchConfig, SearchIndexDefinition indexDefinition, List<string> codeNamesThatShouldExist)
        {
            ISearchServiceClient serviceClient = _searchClientFactory.Get(searchConfig.Name);
            ISearchIndexClient indexClient = serviceClient.Indexes.GetClient(indexDefinition.IndexName);

            List<string> codeNamesThatExistInSearchIndex = new List<string>();

            int maxResultsPerSearch = 1000;

            var indexContents = await indexClient.Documents.SearchAsync("*",
            new SearchParameters()
            {
                Select = new List<string>() { "CodeName" },
                IncludeTotalResultCount = true,
                OrderBy = new List<string>() { "CodeName" },
                Top = maxResultsPerSearch
            });

            int searchPage = 1;
            _logger.LogInformation("Beginning paging through search index to find unique content items: {indexName}", indexDefinition.IndexName);
            while (true)
            {
                foreach (var item in indexContents.Results)
                {
                    codeNamesThatExistInSearchIndex.Add(item.Document["CodeName"].ToString());
                }

                indexContents = await indexClient.Documents.SearchAsync("*",
                    new SearchParameters()
                    {
                        Select = new List<string>() { "CodeName" },
                        IncludeTotalResultCount = true,
                        OrderBy = new List<string>() { "CodeName" },
                        Top = maxResultsPerSearch,
                        Skip = searchPage * maxResultsPerSearch
                    });

                searchPage++;
                if (indexContents.Results.Count == 0) //Any further pages of results to process?
                    break;
            }
            _logger.LogInformation("Finished paging through search index to find unique content items: {indexName}", indexDefinition.IndexName);

            var itemsToRemove = codeNamesThatExistInSearchIndex.Where(p => !codeNamesThatShouldExist.Any(c => c == p)).ToList();

            List<IndexAction<Microsoft.Azure.Search.Models.Document>> lstActions = new List<IndexAction<Microsoft.Azure.Search.Models.Document>>();
            foreach (var indexItem in itemsToRemove)
            {
                var doc = new Microsoft.Azure.Search.Models.Document();
                doc.Add("CodeName", indexItem);
                lstActions.Add(new IndexAction<Microsoft.Azure.Search.Models.Document>() { ActionType = IndexActionType.Delete, Document = doc });
            }

            _logger.LogInformation("Found {num} content items to purge from index: {indexName}", lstActions.Count, indexDefinition.IndexName);
            if (lstActions.Count > 0)
            {
                var batch = IndexBatch.New(lstActions);
                var indexResult = indexClient.Documents.Index(batch); //Request a reindexing request
            }
        }

        private async Task PopulateNewsIndex(AzureSearchConfig searchConfig, SearchIndexDefinition indexDefinition)
        {
            _logger.LogInformation($"Begin Populating News Index: {indexDefinition.IndexName}");
            ISearchServiceClient serviceClient = _searchClientFactory.Get(searchConfig.Name);
            List<IndexAction<Microsoft.Azure.Search.Models.Document>> lstActions = new List<IndexAction<Microsoft.Azure.Search.Models.Document>>();

            var deliveryClient = _clientFactory.Get(searchConfig.Name);

            // Get the content from Kentico Cloud
            var response = await deliveryClient.GetItemsAsync<NewsArticle>(
                new InFilter("system.type", $"{NewsArticle.Codename}"),
                new DepthParameter(100),
                new LimitParameter(100),
                new OrderParameter("system.codename", SortOrder.Descending) //Required to ensure paging is deterministic
            );

            List<string> newsCodenamesThatShouldExist = new List<string>();

            var httpClient = _httpFactory.CreateClient();

            while (true)
            {
                foreach (var item in response.Items)
                {
                    _logger.LogInformation("Fetching {@page} news content to send to Azure Search Service", item.System);

                    string content = string.Empty;

                    try
                    {
                        var rawContentUrl = indexDefinition.RawContentUrls.FirstOrDefault(p => p.Codename == NewsArticle.Codename);
                        var httpResponse = await httpClient.GetAsync(searchConfig.WebsiteHomeUrl + rawContentUrl.Url.Replace("{urlSlug}", item.UrlSlug));
                        var friendlyUrl = httpResponse.Headers.GetValues(Constants.FriendlyUrlResponseHeader).FirstOrDefault();
                        content = await httpResponse.Content.ReadAsStringAsync();
                        newsCodenamesThatShouldExist.Add(item.System.Codename); //Unique kentico identifer for this content item

                        var collapsedContent = HttpUtility.HtmlDecode(content.StripHtml(" ")).CondenseToWholeWords();
                        var doc = new Microsoft.Azure.Search.Models.Document();
                        doc.Add("Content", collapsedContent);
                        doc.Add("CodeName", item.System.Codename);
                        doc.Add("Type", item.System.Type);
                        doc.Add("Title", item.MetadataMetaTitle);
                        doc.Add("Date", item.System.LastModified);
                        doc.Add("UrlSlug", item.UrlSlug);
                        doc.Add("Url", friendlyUrl);
                        doc.Add("Synopsis", item.Synopsis);

                        lstActions.Add(IndexAction.MergeOrUpload(doc));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error Fetching {@page} content to send to Azure Search Service. Page may not yet be available in the site cache.", item.System);
                    }
                }

                if (string.IsNullOrEmpty(response.Pagination.NextPageUrl)) //Any further pages of results to process?
                {
                    break;
                }

                response = await deliveryClient.GetItemsAsync<NewsArticle>(
                        new InFilter("system.type", $"{NewsArticle.Codename}"),
                        new DepthParameter(100),
                        new LimitParameter(100),
                        new SkipParameter(response.Pagination.Skip + response.Pagination.Count),
                        new OrderParameter("system.codename", SortOrder.Descending) //Required to ensure paging is deterministic
                    );
            }


            ISearchIndexClient indexClient = serviceClient.Indexes.GetClient(indexDefinition.IndexName);

            await RemoveStaleIndexContent(searchConfig, indexDefinition, newsCodenamesThatShouldExist);

            if (lstActions.Count > 0)
            {
                var batch = IndexBatch.New(lstActions);
                try
                {
                    var indexResult = indexClient.Documents.Index(batch);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, ex.Message);
                    throw;
                }
            }

            _logger.LogInformation($"Ended Populating News Index: {indexDefinition.IndexName}");
        }

        private async Task PopulateAssetsBlobStorage(AzureSearchConfig searchConfig, SearchIndexDefinition indexDefinition)
        {
            _logger.LogInformation("Begin Populating Assets Blob Storage");

            var managementClient = _managementClientFactory.Get(searchConfig.Name);

            var response = await managementClient.ListAssetsAsync();
            var downloadClient = _httpFactory.CreateClient();

            var blobServiceClient = _blobClientFactory.CreateClient(searchConfig.Name);
            var containerClient = blobServiceClient.GetBlobContainerClient(searchConfig.AzureBlobContainerName);

            List<string> fileNamesThatShouldExistInBlobStorage = new List<string>();

            while (true)
            {
                foreach (var asset in response)
                {
                    var fileInfo = new FileInfo(asset.FileName);
                    if (indexDefinition.FileTypesToIndex.Contains(fileInfo.Extension))
                    {
                        var url = new Uri(asset.Url);

                        _logger.LogInformation("Fetching {@page} blob content to send to Azure Search Service", url);

                        string blobUniqueFileName = HttpUtility.UrlDecode(url.AbsolutePath.Substring(1, url.AbsolutePath.Length - 1).Replace("/", "_"));

                        //Check MD5 hash to see if it needs to be reuploaded to azure blob storage
                        var blobClient = containerClient.GetBlobClient(blobUniqueFileName);

                        //https://stackoverflow.com/questions/14899461/invalid-character-exception-when-adding-metadata-to-a-cloudblob
                        //Only ascii characters can be placed into metadata.
                        var metaData = new Dictionary<string, string>();
                        metaData.Add("FileNameBase64", asset.FileName.ToBase64()); //Azure blob metadata doesn't support metadata with non-ascii characters...
                        metaData.Add("CDNUrlBase64", asset.Url.ToBase64());
                        if (!string.IsNullOrEmpty(asset.Title))
                            metaData.Add("KenticoTitleBase64", asset.Title.ToBase64());
                        metaData.Add("KenticoLastModified", asset.LastModified.Value.ToString("o", CultureInfo.InvariantCulture));
                        metaData.Add("KenticoId", asset.Id.ToString());

                        foreach (var desc in asset.Descriptions.Where(p => p.Description != null).ToList())
                        {
                            var language = desc.Language.Codename == null ? "default" : desc.Language.Codename;
                            metaData.Add("KenticoDescriptionBase64_" + language, desc.Description.ToBase64()); //Get keywords for description in there. azure blob metadata doesn't support non-ascii characters
                        }

                        var blobExists = await blobClient.ExistsAsync();
                        if (!blobExists.Value)
                        {
                            //Download Asset and upload to Azure Blob Storage so it can be indexed
                            byte[] data = await downloadClient.GetByteArrayAsync(asset.Url);
                            _logger.LogInformation("Created new azure blob storage item. File: {fileName}", blobUniqueFileName);
                            using (var ms = new MemoryStream(data))
                            {
                                var uploadResult = await blobClient.UploadAsync(ms);
                            }

                            var metaResult = await blobClient.SetMetadataAsync(metaData);
                        }
                        else //File already exists, perhaps we just need to update meta data if the KenticoLastModified date is different
                        {
                            //We don't need to deal with MD5 hash mismatches because all asset uploads are a unique blob item, so there is no way it could change with how kentico doesn't it's
                            //asset storage
                            _logger.LogInformation("Refresh metadata in azure blob storage. File: {fileName}", blobUniqueFileName);
                            var properties = await blobClient.GetPropertiesAsync();
                            if (properties.Value.Metadata.ContainsKey("KenticoLastModified"))
                            {
                                DateTime kenticoLastModified = DateTime.ParseExact(properties.Value.Metadata["KenticoLastModified"], "o", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
                                if (kenticoLastModified.Truncate(TimeSpan.TicksPerSecond) != asset.LastModified.Value.Truncate(TimeSpan.TicksPerSecond))
                                {
                                    var metaResult = await blobClient.SetMetadataAsync(metaData);
                                }
                            }
                        }

                        fileNamesThatShouldExistInBlobStorage.Add(blobUniqueFileName);
                    }
                }

                if (!response.HasNextPage())
                {
                    break;
                }

                response = await response.GetNextPage();
            }

            //Now loop through all azure blob storage items, if there are items present that no longer exist in Kentico, then delete them
            //so they get removed from the search index on the next indexer run.
            var blobsToSoftDelete = new List<string>();
            await foreach (BlobItem blob in containerClient.GetBlobsAsync())
            {
                if (!fileNamesThatShouldExistInBlobStorage.Contains(blob.Name))
                {
                    //File shouldn't exist in blob storage anymore. Add it to the delete list.
                    blobsToSoftDelete.Add(blob.Name);
                }
            }

            foreach (var blobToDelete in blobsToSoftDelete)
            {
                var blobClient = containerClient.GetBlobClient(blobToDelete);
                //See if it was already deleted and when that was, if it's beyond the threshold of keeping it as a soft deleted item then permanantly delete it as
                //it has already been removed from the search index
                var blobProperties = await blobClient.GetPropertiesAsync();
                if (blobProperties.Value.Metadata.ContainsKey(searchConfig.SoftDeletedDateField))
                {
                    var dateSoftDeleted = DateTime.ParseExact(blobProperties.Value.Metadata[searchConfig.SoftDeletedDateField], "o", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
                    if (dateSoftDeleted.AddMinutes(searchConfig.PermanentDeleteMinutes) < DateTime.UtcNow)
                    {
                        _logger.LogInformation("Permanently deleted stale blob {filename}", blobToDelete);
                        await blobClient.DeleteAsync();
                    }
                    else
                        _logger.LogInformation("Stale blob {filename} not ready to be permanently deleted yet...", blobToDelete);
                }
                else
                {
                    _logger.LogInformation("Soft Deleted stale blob {filename}", blobToDelete);
                    //Soft delete, this field is set to be monitored by the azure indexer and will remove it from the index. If we outright delete the file, the index won't be refreshed
                    //so we marked it as soft delete / pending deletion so the indexer can remove it from the search index first.
                    var metaData = new Dictionary<string, string>();
                    //Don't wipe out our existing metadata!
                    foreach (var existingMetadata in blobProperties.Value.Metadata)
                    {
                        metaData.Add(existingMetadata.Key, existingMetadata.Value);
                    }
                    metaData.Add(searchConfig.SoftDeletedFieldName, "true");
                    metaData.Add(searchConfig.SoftDeletedDateField, DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
                    await blobClient.SetMetadataAsync(metaData);
                }
            }

            _logger.LogInformation("Ended Populating Assets Blob Storage");
        }
    }
}
