using System;
using System.Collections.Generic;
using System.Linq;
using Kentico.Kontent.Delivery;
using Kentico.Kontent.Delivery.Abstractions;
using Kentico.Kontent.Delivery.ContentItems;

namespace Kentico.Web.Models
{
    public partial class NewsArticle
    {
        public IAsset LinkedImageAsset => Image.FirstOrDefault();
    }
}