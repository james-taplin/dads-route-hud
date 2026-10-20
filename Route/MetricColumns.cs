using System;
using System.Collections.Generic;

namespace DvMod.HeadsUpDisplay
{
    /// <summary>Assigns every enabled metric exactly once, preserving reading order and saved choices.</summary>
    public static class MetricColumns
    {
        /// <summary>
        /// Places metrics into columns. A metric with a saved column keeps it. A metric left on
        /// Auto follows its group, so controls, brakes, power, engine and the rest read as columns
        /// of like things rather than being dealt out in turn. Only groups that actually have a
        /// metric take a column, and they spread across whatever columns exist: with fewer columns
        /// than groups, neighbouring groups share one in order, and with more, the spare columns
        /// are left for explicit choices rather than padded.
        /// </summary>
        public static IReadOnlyList<List<int>> Arrange(IReadOnlyList<int> assignments,
            IReadOnlyList<MetricGroup>? groups, int count)
        {
            count = Math.Max(1, Math.Min(8, count));
            var columns = new List<List<int>>(count);
            for (var c = 0; c < count; c++) columns.Add(new List<int>());

            var present = new List<MetricGroup>();
            if (groups != null)
                for (var group = 0; group < MetricGroups.Count; group++)
                    for (var i = 0; i < assignments.Count && i < groups.Count; i++)
                        if (assignments[i] <= 0 && (int)groups[i] == group)
                        {
                            present.Add((MetricGroup)group);
                            break;
                        }

            for (var i = 0; i < assignments.Count; i++)
            {
                int column;
                if (assignments[i] > 0) column = Math.Min(count, assignments[i]) - 1;
                else if (groups != null && i < groups.Count && present.Count > 0)
                    column = present.IndexOf(groups[i]) * count / present.Count;
                else column = i % count;
                columns[column].Add(i);
            }
            return columns;
        }

        /// <summary>Placement without grouping, dealing metrics out in reading order.</summary>
        public static IReadOnlyList<List<int>> Arrange(IReadOnlyList<int> assignments, int count) =>
            Arrange(assignments, null, count);
    }
}
