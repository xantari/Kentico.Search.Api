using System;
using System.Collections.Generic;
using System.Text;

namespace Kentico.Search.Api.Models.ResponseModels
{
    public class SearchResultResponseModel
    {
        public SearchResultResponseModel()
        {
            Results = new List<SearchResultItemResponseModel>();
        }
        public string SearchTerms { get; set; }
        public long TotalResults { get; set; }

        public string IndexName { get; set; }

        public string IndexFriendlyName { get; set; }

        public List<SearchResultItemResponseModel> Results { get; set; }
    }
}
