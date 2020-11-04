using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Azure;
using Microsoft.Azure.Search;
using Azure.Core;
using Kentico.Kontent.Management;
using Serilog;
using Microsoft.AspNetCore.Http.Extensions;
using Kentico.Search.Api.Models.Settings;
using Kentico.Search.Api.Services;
using Kentico.Search.Api.Services.Factories;
using Kentico.Kontent.Delivery.Abstractions;
using Kentico.Web.Models;
using Kentico.Kontent.Delivery.Caching;
using Kentico.Kontent.Delivery.Builders.DeliveryClient;
using Kentico.Validation.Common.Core.ActionFilter;
using Kentico.Search.Api.Config;
using Microsoft.Azure.Storage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Azure.Storage.Blob;

namespace Kentico.Search.Api
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        private IWebHostEnvironment Environment { get; }

        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            Configuration = configuration;
            Environment = env;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Adds services required for using options.
            services.AddOptions();

            services.Configure<ProjectOptions>(Configuration);
            var projectOptions = Configuration.Get<ProjectOptions>();

            services.AddMemoryCache();

            services.AddKentico(Environment, Configuration, projectOptions);

            //https://github.com/Azure/azure-sdk-for-net/blob/master/sdk/core/Microsoft.Extensions.Azure/README.md
            services.AddAzureClients(builder =>
            {
                // Configure global retry mode
                builder.ConfigureDefaults(options => options.Retry.Mode = RetryMode.Exponential);

                // Advanced configure global defaults
                // builder.ConfigureDefaults((options, provider) => options.AddPolicy(provider.GetService<DependencyInjectionEnabledPolicy>(), HttpPipelinePosition.PerCall));

                foreach (var config in projectOptions.AzureSearchConfigs)
                {
                    // Register blob service client and initialize it using the Storage section of configuration
                    builder.AddBlobServiceClient(config.AzureBlobStorageConnectionString)
                            .WithName(config.Name);
                }
            });

            services.AddSingleton<ISearchServiceClientFactory>(options =>
            {
                var clientFactory = new SearchServiceClientFactory(options);

                foreach (var config in projectOptions.AzureSearchConfigs)
                {
                    //https://github.com/Azure-Samples/search-dotnet-getting-started
                    //https://github.com/Azure/azure-sdk-for-net/tree/master/sdk/search
                    //https://github.com/Azure/azure-sdk-for-net/issues/8709
                    //Register the Azure Search Service client
                    clientFactory.Add(config.Name, new SearchServiceClient(config.SearchServiceName, new SearchCredentials(config.SearchServiceQueryApiKey)));
                }
                return clientFactory;
            });

            services.AddSingleton<IKenticoManagementClientFactory>(options =>
            {
                var clientFactory = new KenticoManagementClientFactory(options);

                foreach (var config in projectOptions.AzureSearchConfigs)
                {
                    clientFactory.Add(config.Name, new ManagementClient(config.ManagementOptions));
                }
                return clientFactory;
            });

            services.AddMvc(option =>
            {
                option.EnableEndpointRouting = false;
                option.Filters.Add(typeof(ApiExceptionFilter));
            });
            //.AddNewtonsoftJson();
            //.AddJsonOptions(options =>
            //options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

            services.AddOpenApiDocument(options =>
            {
                options.PostProcess = document =>
                {
                    document.Info.Version = "v1";
                    document.Info.Title = "Kentico Search Api";
                    document.Info.Description = "Kentico Search API.";
                };

                options.AllowNullableBodyParameters = false;
                options.Title = "Kentico Search Api";
            });

            services.AddScoped<ISearchService, SearchService>();

            services.AddHttpClient(); //Allows for injecting IHttpClientFactory
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseDefaultFiles();
            app.UseStaticFiles(); //So index.html can be displayed
            app.UseRouting();
            app.UseAuthorization();

            app.UseSerilogRequestLogging(options =>
            {
                options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
                {
                    diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
                    diagnosticContext.Set("HttpRequestClientHostIP", httpContext.Connection.RemoteIpAddress);
                    diagnosticContext.Set("HttpRequestUrl", httpContext.Request.GetDisplayUrl());
                };
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            app.UseOpenApi();
            app.UseSwaggerUi3();
        }
    }
}
