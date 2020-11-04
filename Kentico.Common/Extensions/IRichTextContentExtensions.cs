using Kentico.Kontent.Delivery.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Kentico.Common.Extensions
{
    public static class IRichTextContentExtensions
    {
        public static List<T> FindInlineContentTypes<T>(this IRichTextContent content)
        {
            var list = new List<T>();
            foreach (IRichTextBlock block in content.Blocks)
            {
                switch (block)
                {
                    case IInlineContentItem itm:
                        {
                            switch (itm.ContentItem)
                            {
                                case T inlineType:
                                    {
                                        list.Add(inlineType);
                                        break;
                                    }
                            }
                            break;
                        }
                }
            }
            return list;
        }

        /// <summary>
        /// Checks if a richtext area is empty. A special check is required due to blank richtext areas actually have empty html content in it.
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        /// <remarks>
        /// https://docs.kontent.ai/reference/delivery-api#section/Rich-text-element
        /// </remarks>
        public static bool IsEmptyContent(this IRichTextContent content)
        {
            //MRO: 5/8/2020: Put in additional is string null or whitespace in the event that they someday fix this issue (opened support case on it)
            if (content == null || (content.Blocks.Count() == 1 &&
                    (content.Blocks.FirstOrDefault().ToString() == @"<p><br></p>" || string.IsNullOrWhiteSpace(content.Blocks.FirstOrDefault().ToString()))))
                return true;
            return false;
        }
    }

}