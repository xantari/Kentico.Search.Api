using Kentico.Common.Models;
using Microsoft.AspNetCore.Http;
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
    //Look at switching this to the Kentico.Kontent.AspnetCore nuget after they fix this issue:
    //https://github.com/Kentico/kontent-aspnetcore/issues/1
    public class SignatureMiddleware
    {
        private readonly RequestDelegate _next;

        public SignatureMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext httpContext, IOptions<ProjectOptionsBase> projectOptions)
        {
            var request = httpContext.Request;
            request.EnableBuffering();

            using var reader = new StreamReader(request.Body, Encoding.UTF8, true, 1024, true);
            var content = await reader.ReadToEndAsync();
            request.Body.Seek(0, SeekOrigin.Begin);

            // Iterates through all secrets to allow us to use multiple Kentico projects content.
            var generatedSignatures = new List<string>();
            foreach (var sig in projectOptions.Value.KenticoKontentWebhookSecrets)
                generatedSignatures.Add(GenerateHash(content, sig));

            var signature = request.Headers["X-KC-Signature"].FirstOrDefault();

            if (!generatedSignatures.Contains(signature))
            {
                httpContext.Response.StatusCode = 401;
                return;
            }

            await _next(httpContext);
        }

        private static string GenerateHash(string message, string secret)
        {
            secret ??= "";
            var safeUtf8 = new UTF8Encoding(false, true);
            var keyBytes = safeUtf8.GetBytes(secret);
            var messageBytes = safeUtf8.GetBytes(message);

            using var hmacsha256 = new HMACSHA256(keyBytes);
            var hashMessage = hmacsha256.ComputeHash(messageBytes);

            return Convert.ToBase64String(hashMessage);
        }
    }
}
