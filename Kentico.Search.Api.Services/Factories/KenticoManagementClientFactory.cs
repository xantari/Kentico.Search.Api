using Kentico.Kontent.Management;
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
    public interface IKenticoManagementClientFactory
    {
        void Add(string name, ManagementClient client);
        ManagementClient Get(string name);
        ManagementClient Get();
    }

    public class KenticoManagementClientFactory : IKenticoManagementClientFactory
    {
        private ConcurrentDictionary<string, ManagementClient> _clients = new ConcurrentDictionary<string, ManagementClient>();
        private readonly IServiceProvider _serviceProvider;

        public KenticoManagementClientFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public ManagementClient Get(string name)
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
        /// When you register just one delivery client you must register the ManagementClient singleton in startup.cs
        /// </summary>
        /// <returns></returns>
        public ManagementClient Get()
        {
            return _serviceProvider.GetRequiredService<ManagementClient>();
        }

        public void Add(string name, ManagementClient client)
        {
            _clients.TryAdd(name, client);
        }
    }
}
