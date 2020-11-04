using Kentico.Search.Api.Client.Config;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Kentico.Search.Api.Client
{
    public abstract class ClientBase
    {
        protected KenticoSearchApiClientConfig _clientConfig;

        public ClientBase()
        {
        }

        public string BaseUrl
        {
            get { return KenticoSearchApiClientConfig.ApiEndpoint; }
        }

        protected async Task<HttpRequestMessage> CreateHttpRequestMessageAsync(CancellationToken cancellationToken) => await Task.FromResult(new HttpRequestMessage());
    }
}