using System.Collections.Generic;

namespace Kentico.Search.Api.Models.ResponseModels
{
    public class SearchServiceResponseModel
    {
        public SearchServiceResponseModel()
        {
            Indexes = new List<SearchIndexDefinitionResponseModel>();
        }

        public string Name { get; set; }
        public List<SearchIndexDefinitionResponseModel> Indexes { get; set; }

    }
}
