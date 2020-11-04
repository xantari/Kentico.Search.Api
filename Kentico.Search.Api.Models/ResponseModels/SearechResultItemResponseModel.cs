using System;
using System.Collections.Generic;
using System.Text;

namespace Kentico.Search.Api.Models.ResponseModels
{
    public class SearchResultItemResponseModel
    {
        public SearchResultItemResponseModel()
        {
            Highlights = new List<string>();
        }
        public string Title { get; set; }
        public string Url { get; set; }
        public string Content { get; set; }
        public double Score { get; set; }
        public bool IsBlob { get; set; }
        public List<string> Highlights { get; set; }
    }
}
