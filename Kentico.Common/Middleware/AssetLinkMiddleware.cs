using Kentico.Common.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Kentico.Common.Middleware
{
    /// <summary>
    /// Asset Link Middleware
    /// 2/27/2020 - MRO: Custom middleware that alters the response body to make the kentico assets show up from our own domain.
    /// The middleware rewrites the asset links to point to our /Assets controller and passes in the query string verbatim that is used from their CDN. Our asset controller
    /// then fetches that content from Kentico and serves it up from our domain. The reason why is so that all PDF's and other assets appear to come from our domain for SEO
    /// purposes (not 100% sure if this is required for good SEO, and if it isn't, we can remove this middleware in the future as this causes a bit of overhead)
    //  See: https://github.com/Kentico/kontent-delivery-sdk-net/issues/193
    //  https://jeremylindsayni.wordpress.com/2019/02/18/adding-middleware-to-your-net-core-mvc-pipeline-that-formats-and-indents-html-output/
    //  https://docs.microsoft.com/en-gb/aspnet/core/fundamentals/middleware/index?view=aspnetcore-2.2
    /// </summary>
    public class AssetLinkMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AssetLinkMiddleware> _logger;

        public AssetLinkMiddleware(RequestDelegate next, ILogger<AssetLinkMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext httpContext, IOptions<ProjectOptionsBase> projectOptions)
        {
            var body = httpContext.Response.Body;

            using (var updatedBody = new MemoryStream())
            {
                httpContext.Response.Body = updatedBody; 

                await _next(httpContext);

                httpContext.Response.Body = body;

                updatedBody.Seek(0, SeekOrigin.Begin);

                if (httpContext.Response.ContentType != null && httpContext.Response.ContentType.Contains("text/html")) //Only manipulate the html data coming back, ignore all other file types (such as jpg, png, etc)
                {

                    var newContent = new StreamReader(updatedBody).ReadToEnd();

                    string assetUrl = projectOptions.Value.AssetUrl;
                    string[] kenticoCdnUrls = projectOptions.Value.KenticoAssetCDNUrls;

                    for (int i = 0; i < kenticoCdnUrls.Length; i++)
                        newContent = newContent.Replace(kenticoCdnUrls[i], assetUrl);

                    //If you remove this Clear, and you have a static file such as healthcheck.htm, and there are byte order marks in the file (there are), then repeated hits to the file
                    //appear to cause problems writing the content as it will be missing the UTF-8 BOM (EF BB BF) 3 bytes. It causes the length to be incorrect by 3 bytes.
                    //The reason being is that the streamreader above actually strips the byte order marks and for static files they are already in the response output in the pipeline
                    //so we clear the result to ensure the ContentLength is correct. Could not figure out why the streamreader actually strips the byte order marks.
                    //The detect encoding option on StreamReader for some reason was not detecting the byte order marks.
                    if (httpContext.Response.ContentLength != null)
                        httpContext.Response.Clear();
                    await httpContext.Response.WriteAsync(newContent);
                }
                else //Return the asset bytes (jpg, pdf, other binary content)
                {
                    await updatedBody.CopyToAsync(httpContext.Response.Body);
                }
            }
        }
    }
}
