using Kentico.Common.Models;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.AspNetCore.WebUtilities;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Kentico.Common.Services
{
    /// <summary>
    /// Kentico Asset Service
    /// Provides a way to get information about kentico assets to serve up from our own domain
    /// </summary>
    /// <remarks>
    /// 2/27/2020 - MRO: Initial creation
    /// </remarks>
    public class AssetService : IAssetService
    {
        private readonly HttpClient _httpClient;
        private readonly IMimeMappingService _mimeMappingService;

        public AssetService(HttpClient httpClient, IMimeMappingService mimeMappingService)
        {
            _httpClient = httpClient;
            _mimeMappingService = mimeMappingService;
        }

        public async Task<KenticoAsset> GetKenticoAsset(string[] cdnUrls, string urlFragement)
        {
            var asset = new KenticoAsset();

            //Build all the original possible kentico cdn Urls
            List<string> cdnFullUrls = new List<string>();
            foreach (var itm in cdnUrls)
            {
                cdnFullUrls.Add($"https://{itm}/{urlFragement}");
            }

            var result = await _httpClient.GetAsync(cdnFullUrls[0]);
            if (!result.IsSuccessStatusCode)
                return null;

            byte[] data = await result.Content.ReadAsByteArrayAsync();
            //byte[] data = await _httpClient.GetByteArrayAsync(cdnFullUrls[0]);

            string fileName = Path.GetFileName(urlFragement);

            asset.ContentType = _mimeMappingService.Map(fileName);
            asset.FileData = data;
            asset.FileName = fileName;
            asset.EtagCheckshum = CalculateChecksum(data);
            asset.KenticoUrls = cdnFullUrls.ToArray();

            return asset;
        }

        private static string CalculateChecksum(byte[] data)
        {
            string checksum = "";
            using (MemoryStream ms = new MemoryStream(data))
            {
                using (var algo = SHA1.Create())
                {
                    ms.Position = 0;
                    byte[] bytes = algo.ComputeHash(ms);
                    checksum = $"\"{WebEncoders.Base64UrlEncode(bytes)}\"";
                }
            }

            return checksum;
        }
    }

    public interface IAssetService
    {
        Task<KenticoAsset> GetKenticoAsset(string[] cdnUrls, string urlFragement);
    }
}
