using System;
using System.Collections.Generic;
using System.Text;

namespace Kentico.Search.Api.Client.Config
{
    public class KenticoSearchApiClientConfig
    {
        //Static as it gets set upon application startup from the DashboardApiClientConfig via the .NET Core startup routine and is
        //used within BaseUrl in ClientBase so that all methods in the HttpClient's that are injected in go to the proper URL
        public static string ApiEndpoint { get; set; }
    }
}
