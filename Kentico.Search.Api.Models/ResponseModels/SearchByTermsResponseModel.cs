using System;
using System.Collections.Generic;
using System.Text;

namespace Kentico.Search.Api.Models.ResponseModels
{
    public class SearchByTermsResponseModel
    {
        public SearchByTermsResponseModel()
        {
            Results = new List<SearchResultResponseModel>();
        }
        public string SearchTerms { get; set; }
        public long TotalResults { get; set; }

        public List<SearchResultResponseModel> Results { get; set; }
    }
}
