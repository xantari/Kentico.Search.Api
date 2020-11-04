using Kentico.Kontent.Delivery;
using Kentico.Kontent.Delivery.Abstractions;
using Kentico.Kontent.Delivery.TaxonomyGroups;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kentico.Common.Extensions
{
    public static class TaxonomyExtensions
    {
        public static string GetHierachicalTaxonomy(this IDeliveryTaxonomyResponse taxonomy, string stopKey)
        {
            return taxonomy.Taxonomy.GetHierachicalTaxonomy(stopKey);
        }

        public static string GetHierachicalTaxonomy(this ITaxonomyGroup taxonomy, string stopKey, bool removeTrailingSlash = false)
        {
            if (string.IsNullOrEmpty(stopKey) || taxonomy == null)
                return string.Empty;

            var taxonomyString = string.Empty;
            if (taxonomy.Terms.Count > 0)
            {
                taxonomyString = GetHierachicalTaxonomyDetail(taxonomy.Terms, stopKey);
            }
            if (removeTrailingSlash && taxonomyString.EndsWith("/"))
            {
                return taxonomyString.Substring(0, taxonomyString.Length - 1);
            }
            return taxonomyString;
        }

        public static string GetHierachicalTaxonomyDetail(this IList<ITaxonomyTermDetails> taxonomy, string stopKey)
        {
            if (string.IsNullOrEmpty(stopKey) || taxonomy == null)
                return string.Empty;

            StringBuilder sb = new StringBuilder();

            if (taxonomy.Count > 0)
            {
                foreach (var taxon in taxonomy)
                {
                    if (taxon.Codename == stopKey)
                    {
                        sb.Append(taxon.Name.URLFriendly() + "/");
                        return sb.ToString();
                    }
                    if (IsStopKeyInThisSubtree(taxon.Terms, stopKey))
                    {
                        sb.Append(taxon.Name.URLFriendly() + "/");
                        sb.Append(GetHierachicalTaxonomyDetail(taxon.Terms, stopKey));
                    }
                }
            }
            return sb.ToString();
        }

        public static bool IsStopKeyInThisSubtree(this IList<ITaxonomyTermDetails> taxonomy, string stopKey)
        {
            foreach (var taxon in taxonomy)
            {
                if (taxon.Codename == stopKey)
                    return true;
                bool isInThisSubtree = taxon.Terms.Any(p => p.Codename == stopKey);
                if (isInThisSubtree)
                    return true;
                if (taxon.Terms.Count > 0 && !isInThisSubtree)
                {
                    isInThisSubtree = IsStopKeyInThisSubtree(taxon.Terms, stopKey);
                    if (isInThisSubtree)
                        return true;
                }
            }
            return false;
        }
    }
}
