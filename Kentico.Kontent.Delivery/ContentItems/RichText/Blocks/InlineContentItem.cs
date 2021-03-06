﻿using System.Collections;
using System.Collections.Generic;
using Kentico.Kontent.Delivery.Abstractions;

namespace Kentico.Kontent.Delivery.ContentItems.RichText.Blocks
{
    /// <summary>
    /// Represents inline content item. IEnumerable is implemented so that Html.DisplayFor is automatically bridged to the underlying ContentItem property.
    /// </summary>
    internal class InlineContentItem : IInlineContentItem, IEnumerable<object>
    {
        public object ContentItem
        {
            get;
        }

        public InlineContentItem(object contentItem)
        {
            ContentItem = contentItem;
        }

        public IEnumerator<object> GetEnumerator()
        {
            yield return ContentItem;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            yield return ContentItem;
        }
    }
}
