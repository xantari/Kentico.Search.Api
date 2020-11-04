using System.Linq;
using Kentico.Kontent.Delivery;
using Kentico.Kontent.Delivery.Abstractions;

namespace Kentico.Common.Models
{
    public class ProjectOptionsBase
    {
        public DeliveryOptions DeliveryOptions { get; set; }

        public string[] KenticoKontentWebhookSecrets { get; set; }

        public int[] ResponsiveWidths { get; set; }

        public bool ResponsiveImagesEnabled => ResponsiveWidths != null && ResponsiveWidths.Any();

        public string[] KenticoAssetCDNUrls { get; set; }
        public string AssetUrl { get; set; }
    }
}
