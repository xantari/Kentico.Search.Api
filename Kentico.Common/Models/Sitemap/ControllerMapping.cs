using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kentico.Common.Models.Sitemap
{
    public class ControllerMapping
    {
        public ControllerMapping() { }

        public string ControllerName { get; set; }
        public string FullUrl { get; set; }
        public string PageUrlSlug { get; set; }
        public string PageCodeName { get; set; }
    }
}
