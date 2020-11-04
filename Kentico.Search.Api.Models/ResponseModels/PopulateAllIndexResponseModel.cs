﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Kentico.Search.Api.Models.ResponseModels
{
    public class PopulateAllIndexResponseModel
    {
        public PopulateAllIndexResponseModel()
        {
            Messages = new List<string>();
        }
        public bool Success { get; set; }
        public string IndexName { get; set; }
        public List<string> Messages { get; set; }
    }
}
