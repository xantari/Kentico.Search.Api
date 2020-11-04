using System;
using System.Collections.Generic;
using System.Text;
using Kentico.Search.Api.Models.Enums;
using Azure.Storage.Blobs;
using Kentico.Kontent.Delivery;
using Kentico.Kontent.Delivery.Abstractions;
using Kentico.Kontent.Management;

namespace Kentico.Search.Api.Models.Settings
{
    public class AzureSearchConfig
    {
        public AzureSearchConfig()
        {
            IndexDefinitions = new List<SearchIndexDefinition>();
        }

        /// <summary>
        /// The unique name of this search configuration
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Azure Search Service Name
        /// </summary>
        public string SearchServiceName { get; set; }
        /// <summary>
        /// Azure Search Service Api Key
        /// </summary>
        public string SearchServiceQueryApiKey { get; set; }

        public string AssetsControllerName { get; set; }

        public List<SearchIndexDefinition> IndexDefinitions { get; set; }

        public ManagementOptions ManagementOptions { get; set; }

        //Kentico Delivery Client Setup
        public DeliveryOptions DeliveryOptions { get; set; }

        public string AzureBlobStorageConnectionString { get; set; }
        public string AzureBlobContainerName { get; set; }
        public string SoftDeletedFieldName { get; set; }
        public string SoftDeletedDateField { get; set; }
        public string WebsiteHomeUrl { get; set; }
        public int PermanentDeleteMinutes { get; set; }
        public string AzureBlobDatasourceName { get; set; }
        public TimeSpan BlobIndexerRunInterval { get; set; }
        public string AzureBlobIndexerName { get; set; }
    }

    public class SearchIndexDefinition
    {
        public SearchIndexDefinition()
        {
            AdditionalFields = new List<string>();
            RawContentUrls = new List<RawContentUrl>();
            FileTypesToIndex = new List<string>();
        }

        /// <summary>
        /// Azure Search Index Name
        /// </summary>
        public string IndexName { get; set; }
        public string IndexFriendlyName { get; set; }

        public IndexType IndexType { get; set; }

        public ContentType ContentType { get; set; }

        public List<RawContentUrl> RawContentUrls { get; set; }

        public string UrlField { get; set; }
        public string ContentField { get; set; }
        public string TitleField { get; set; }

        public List<string> AdditionalFields { get; set; }
        public string DescriptionField { get; set; }

        //Azure Blob Storage specific fields begin here
        public string FileNameField { get; set; }
        public List<string> FileTypesToIndex { get; set; }
    }

    public class RawContentUrl
    {
        public RawContentUrl() { }
        public string Url { get; set; }
        public string Codename { get; set; }
    }


}
