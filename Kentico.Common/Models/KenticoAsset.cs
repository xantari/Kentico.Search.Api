using System;
using System.Collections.Generic;
using System.Text;

namespace Kentico.Common.Models
{
    [Serializable]
    public class KenticoAsset
    {
        public KenticoAsset() {
            LastFetchDateUtc = DateTime.UtcNow;
        }

        public string[] KenticoUrls { get; set; }
        public byte[] FileData { get; set; }
        public string FileName { get; set; }
        /// <summary>
        /// Mime Type
        /// </summary>
        public string ContentType { get; set; }
        public string EtagCheckshum { get; set; }

        public DateTimeOffset? LastFetchDateUtc { get; set; }
    }
}
