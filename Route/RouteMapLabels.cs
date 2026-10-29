using System;
using System.Collections.Generic;

namespace DvMod.HeadsUpDisplay
{
    public sealed class RouteMapLabel
    {
        public RouteEvent Event { get; }
        public float X { get; }
        public float Width { get; }
        public int Lane { get; }
        public int Additional { get; internal set; }
        public RouteMapLabel(RouteEvent item, float x, float width, int lane)
        { Event = item; X = x; Width = width; Lane = lane; }
    }

    /// <summary>Fixed lanes; dense overflow is counted explicitly rather than hidden or overlapped.</summary>
    public static class RouteMapLabels
    {
        public static List<RouteMapLabel> Arrange(IReadOnlyList<RouteEvent> events, double start, double end,
            float width, int lanes, Func<RouteEvent, float> measure)
            => ArrangeBoards(events, start, end, width, lanes, measure,
                item => item.Kind == RouteEventKind.Speed && item.SpeedLimitKph.HasValue && item.AppliesToTravel);

        public static List<RouteMapLabel> ArrangeBoards(IReadOnlyList<RouteEvent> events, double start, double end,
            float width, int lanes, Func<RouteEvent, float> measure, Func<RouteEvent, bool> visible, bool includeEnd = true)
        {
            var result = new List<RouteMapLabel>();
            if (lanes <= 0 || width <= 0 || end <= start) return result;
            var last = new RouteMapLabel?[lanes];
            foreach (var item in events)
            {
                if (!visible(item) || item.Distance < start || item.Distance > end
                    || !includeEnd && item.Distance == end) continue;
                var size = Math.Max(1, Math.Min(width, measure(item)));
                var x = Math.Max(0, Math.Min(width - size, (float)RouteGraphScale.Fraction(item.Distance, start, end) * width - size / 2));
                var lane = 0;
                while (lane < lanes && last[lane] != null && last[lane]!.X + last[lane]!.Width + 3 > x) lane++;
                if (lane == lanes)
                {
                    var nearest = last[0]!;
                    for (var index = 1; index < last.Length; index++)
                        if (last[index]!.X > nearest.X) nearest = last[index]!;
                    nearest.Additional++;
                    continue;
                }
                var label = new RouteMapLabel(item, x, size, lane);
                last[lane] = label; result.Add(label);
            }
            return result;
        }
    }
}
