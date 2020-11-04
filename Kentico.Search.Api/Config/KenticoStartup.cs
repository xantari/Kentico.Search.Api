using Kentico.Search.Api.Models.Settings;
using Kentico.Web.Models;
using Kentico.Kontent.Delivery.Abstractions;
using Kentico.Kontent.Delivery.Caching;
using Kentico.Kontent.Delivery.Caching.Extensions;
using Kentico.Kontent.Delivery.Extensions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kentico.Search.Api.Config
{
    public static class KenticoStartup
    {
        public static void AddKentico(this IServiceCollection services, IWebHostEnvironment environment, IConfiguration configuration, ProjectOptions projectOptions)
        {
            var cacheOptions = new DeliveryCacheOptions();
            cacheOptions.CacheType = CacheTypeEnum.Memory;
            //The preview and dev environments turn off caching so that the preview button works within Kentico Kontent
            if (environment.IsDevelopment() || environment.IsEnvironment("LocalDevelopment"))
            {
                cacheOptions.StaleContentExpiration = TimeSpan.FromSeconds(1);
                cacheOptions.DefaultExpiration = TimeSpan.FromSeconds(1);
            }
            else //Production caches everything 
            {
                cacheOptions.StaleContentExpiration = TimeSpan.FromSeconds(2);
                cacheOptions.DefaultExpiration = TimeSpan.FromHours(24);
            }
            services.Configure<DeliveryCacheOptions>((options) =>
            {
                options = cacheOptions;
            });

            services.AddSingleton<ITypeProvider, CustomTypeProvider>();

            //https://github.com/Kentico/kontent-delivery-sdk-net/wiki/Registering-the-DeliveryClient-to-the-IServiceCollection-in-ASP.NET-Core#registering-multiple-clients
            foreach(var config in projectOptions.AzureSearchConfigs)
            {
                services.AddDeliveryClient(config.Name, config.DeliveryOptions);
                services.AddDeliveryClientCache(config.Name, cacheOptions);
            }
        }
    }
}
