using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace DvMod.HeadsUpDisplay
{
    internal enum HudTimingStage
    {
        HudUpdate, RouteUpdate, RouteWalk, RouteCollectStep, RouteFinish, RoutePublish, RouteMerge, RouteShift,
        RouteProfile, OptionalSignals, JunctionNames, ListLayout, RouteTraffic,
        SignDiscoveryNativePlacers, SignDiscoveryDoubleTrackPlacers, SignDiscoveryDebugRoots,
        SignDiscoveryGeneratorRoots, SignIndexRoots, SignMaintenance, SignRootRefresh,
        SignBootstrap, SignProjection, Count
    }

    /// <summary>Logging-only counters. No per-call strings, allocations, file writes or Harmony patches.</summary>
    internal static class HudTimings
    {
        private struct Sample
        {
            public long Ticks, Maximum;
            public int Calls, OverBudget;
        }

        private static readonly Sample[] samples = new Sample[(int)HudTimingStage.Count];
        private static readonly double millisecondsPerTick = 1000d / Stopwatch.Frequency;
        private static double nextReport;
        private static int generation;
        private static bool enabled;
        public static bool Enabled
        {
            get => enabled;
            set
            {
                if (enabled && !value) Reset();
                enabled = value;
            }
        }

        public static Scope Measure(HudTimingStage stage) => new Scope(stage);

        public readonly struct Scope : IDisposable
        {
            private readonly HudTimingStage stage;
            private readonly long started;
            private readonly int sampleGeneration;
            internal Scope(HudTimingStage stage)
            {
                this.stage = stage;
                started = Enabled ? Stopwatch.GetTimestamp() : 0;
                sampleGeneration = generation;
            }
            public void Dispose()
            {
                if (started == 0 || !Enabled || sampleGeneration != generation) return;
                var elapsed = Stopwatch.GetTimestamp() - started;
                ref var sample = ref samples[(int)stage];
                sample.Ticks += elapsed;
                sample.Maximum = Math.Max(sample.Maximum, elapsed);
                sample.Calls++;
                if (elapsed * millisecondsPerTick > 2) sample.OverBudget++;
            }
        }

        public static void Reset()
        {
            Array.Clear(samples, 0, samples.Length);
            nextReport = 0;
            generation++;
        }

        /// <summary>One aggregate per five seconds while existing HUD logging is enabled. Stages overlap.</summary>
        public static string? TakeReport(double now)
        {
            if (!Enabled) return null;
            if (double.IsNaN(now) || double.IsInfinity(now)) return null;
            if (now < nextReport && now >= nextReport - 5) return null;
            nextReport = now + 5;
            StringBuilder? text = null;
            for (var index = 0; index < samples.Length; index++)
            {
                var sample = samples[index];
                if (sample.Calls == 0) continue;
                if (text == null) text = new StringBuilder("HUD timings (inclusive; do not add): ");
                else text.Append("; ");
                text.Append((HudTimingStage)index).Append(" max=")
                    .Append((sample.Maximum * millisecondsPerTick).ToString("F2", CultureInfo.InvariantCulture))
                    .Append("ms total=").Append((sample.Ticks * millisecondsPerTick).ToString("F2", CultureInfo.InvariantCulture))
                    .Append("ms calls=").Append(sample.Calls).Append(" over2ms=").Append(sample.OverBudget);
            }
            Array.Clear(samples, 0, samples.Length);
            return text?.ToString();
        }
    }
}
