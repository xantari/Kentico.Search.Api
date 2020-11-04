using System;
using System.Collections.Generic;
using System.Text;

namespace Kentico.Search.Api.Models.RequestModels
{
    public class PopulateIndexRequestModel
    {
        /// <summary>
        /// Driven by AzureSearchConfig.Name in the sites configuration file. Possible values: "yourkenticosite.org Search"
        /// </summary>
        public string SearchServiceName { get; set; }
        /// <summary>
        /// Search Index Name
        /// </summary>
        public string IndexName { get; set; }
    }
}
