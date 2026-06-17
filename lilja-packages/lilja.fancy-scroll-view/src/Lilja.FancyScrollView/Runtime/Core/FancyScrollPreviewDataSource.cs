/*
 * FancyScrollView (https://github.com/setchi/FancyScrollView)
 * Copyright (c) 2020 setchi
 * Licensed under MIT (https://github.com/setchi/FancyScrollView/blob/master/LICENSE)
 */

namespace Lilja.FancyScrollView
{
    /// <summary>
    /// Context passed when creating edit-mode preview data.
    /// </summary>
    public readonly struct FancyScrollPreviewItemContext
    {
        public FancyScrollPreviewItemContext(int index, int count)
        {
            Index = index;
            Count = count;
        }

        public int Index { get; }

        public int Count { get; }
    }

}
