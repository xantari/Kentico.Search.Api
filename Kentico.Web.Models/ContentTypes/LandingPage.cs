using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kentico.Web.Models.ContentTypes;
using Kentico.Kontent.Delivery;

namespace Kentico.Web.Models
{
    public partial class LandingPage
    {
        public BodyLayoutType BodyLayoutType
        {
            get
            {
                if (BodyLayout.FirstOrDefault()?.Name == Constants.BodyLayoutNormal)
                    return BodyLayoutType.NormalLayout;
                if (BodyLayout.FirstOrDefault()?.Name == Constants.BodyLayoutRaw)
                    return BodyLayoutType.RawLayout;
                return BodyLayoutType.NormalLayout; //Default of normal layout for those pages where it's not defined.
            }
        }
    }
}