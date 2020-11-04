using System;
using System.Collections.Generic;
using System.Text;

namespace Kentico.Search.Api.Models.RequestModels
{
    public class SearchByTermsRequestModel
    {
        /// <summary>
        /// Driven by AzureSearchConfig.Name in the sites configuration file. Possible values: "yourkenticosite.org Search"
        /// </summary>
        public string SearchServiceName { get; set; }
        /// <summary>
        /// Search terms or search syntax string
        /// </summary>
        public string SearchTerms { get; set; }
        /// <summary>
        /// CSS class to wrap hit highlighted results
        /// </summary>
        public string HighlightCssClass { get; set; }
    }
}
