using Microsoft.Azure.Search;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Kentico.Search.Api.Services.Factories
{
    /// <summary>
    /// Azure Search Service Client Factory.
    /// Allows for multiple registrations of different search service clients. It seems Microsoft has not yet added this to their Microsoft.Extensions.Azure libraries
    /// for the search service :(
    /// https://github.com/Azure/azure-sdk-for-net/issues/8709
    /// </summary>
    public interface ISearchServiceClientFactory
    {
        void Add(string name, ISearchServiceClient client);
        ISearchServiceClient Get(string name);
        ISearchServiceClient Get();
    }

    public class SearchServiceClientFactory : ISearchServiceClientFactory
    {
        private ConcurrentDictionary<string, ISearchServiceClient> _clients = new ConcurrentDictionary<string, ISearchServiceClient>();
        private readonly IServiceProvider _serviceProvider;

        public SearchServiceClientFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public ISearchServiceClient Get(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (!_clients.TryGetValue(name, out var client))
            {
                throw new KeyNotFoundException(name);
            }

            return client;
        }

        /// <summary>
        /// Only useful if you register just one delivery client but want to use the factory in case you need to add additional delivery clients later. 
        /// When you register just one delivery client you must register the ISearchServiceClient singleton in startup.cs
        /// </summary>
        /// <returns></returns>
        public ISearchServiceClient Get()
        {
            return _serviceProvider.GetRequiredService<ISearchServiceClient>();
        }

        public void Add(string name, ISearchServiceClient client)
        {
            _clients.TryAdd(name, client);
        }
    }
}
