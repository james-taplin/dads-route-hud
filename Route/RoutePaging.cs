using System;
using System.Collections.Generic;

namespace DvMod.HeadsUpDisplay
{
    public sealed class RoutePage<T>
    {
        public IReadOnlyList<T> Items { get; }
        public int PageIndex { get; }
        public int PageCount { get; }
        public int TotalCount { get; }
        public int FirstItemIndex { get; }

        internal RoutePage(IReadOnlyList<T> items, int pageIndex, int pageCount, int totalCount, int firstItemIndex)
        {
            Items = items;
            PageIndex = pageIndex;
            PageCount = pageCount;
            TotalCount = totalCount;
            FirstItemIndex = firstItemIndex;
        }
    }

    public static class RoutePaging
    {
        /// <summary>Pages a retained catalogue without changing, filtering or capping it.</summary>
        public static RoutePage<T> GetPage<T>(IReadOnlyList<T> source, int pageIndex, int pageSize)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (pageSize <= 0) throw new ArgumentOutOfRangeException(nameof(pageSize));
            var pageCount = source.Count == 0 ? 0 : 1 + (source.Count - 1) / pageSize;
            var actualPage = pageCount == 0 ? 0 : Math.Max(0, Math.Min(pageIndex, pageCount - 1));
            var first = actualPage * pageSize;
            var length = Math.Min(pageSize, source.Count - first);
            var items = new T[length];
            for (var i = 0; i < length; i++) items[i] = source[first + i];
            return new RoutePage<T>(Array.AsReadOnly(items), actualPage, pageCount, source.Count, first);
        }
    }
}
