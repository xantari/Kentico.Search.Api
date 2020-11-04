using System;
using System.Collections.Generic;
using System.Text;

namespace Kentico.Search.Api.Models.ResponseModels
{
    public class AllSearchServiceResponseModel
    {
        public AllSearchServiceResponseModel()
        {
            SearchServices = new List<SearchServiceResponseModel>();
        }

        public List<SearchServiceResponseModel> SearchServices { get; set; }
    }
}
