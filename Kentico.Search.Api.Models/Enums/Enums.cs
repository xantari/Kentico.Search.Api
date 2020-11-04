using System;
using System.Collections.Generic;
using System.Text;

namespace Kentico.Search.Api.Models.Enums
{
    public enum IndexType
    {
        KenticoKontent = 1,
        AzureBlobStorage = 2
    }

    public enum ContentType
    {
        KenticoPageContent = 1,
        KenticoNewsContent = 2,
        AzureBlob = 3
    }
}
