using System;
using System.Collections.Generic;

namespace DvMod.HeadsUpDisplay
{
    /// <summary>Assigns every enabled metric exactly once, preserving reading order and saved choices.</summary>
    public static class MetricColumns
    {
        public static IReadOnlyList<List<int>> Arrange(IReadOnlyList<int> assignments, int count)
        {
            count = Math.Max(1, Math.Min(8, count));
            var columns = new List<List<int>>(count);
            for (var c = 0; c < count; c++) columns.Add(new List<int>());
            for (var i = 0; i < assignments.Count; i++)
                columns[assignments[i] <= 0 ? i % count : Math.Min(count, assignments[i]) - 1].Add(i);
            return columns;
        }
    }
}
