using Kentico.Search.Api.Models.Enums;

namespace Kentico.Search.Api.Models.ResponseModels
{
    public class SearchIndexDefinitionResponseModel
    {
        public SearchIndexDefinitionResponseModel()
        {

        }

        /// <summary>
        /// Azure Search Index Name
        /// </summary>
        public string IndexName { get; set; }
        public string IndexFriendlyName { get; set; }

        public IndexType IndexType { get; set; }

        public ContentType ContentType { get; set; }
    }
}
