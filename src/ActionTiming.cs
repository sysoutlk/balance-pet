// User-facing timing stays simple: one duration and one interval. Small
// natural variation is applied internally and is intentionally not configurable.
using System;

namespace BalancePet
{
    internal static class ActionTiming
    {
        public const double DurationJitter = 0.10;
        public const double PauseJitter = 0.20;

        public static double DurationScale(Config cfg, bool pinned)
        {
            double scale = cfg.GetNumber("actions", "durationScale", 1.0);
            if (scale < 0.5) scale = 0.5;
            if (scale > 3) scale = 3;
            if (pinned) return scale;
            return scale * (1 + (Program.Rng.NextDouble() * 2 - 1) * DurationJitter);
        }

        public static double PauseSeconds(Config cfg)
        {
            double seconds = cfg.GetNumber("actions", "pauseMinSec", 14);
            if (seconds < 3) seconds = 3;
            if (seconds > 300) seconds = 300;
            return seconds * (1 + Program.Rng.NextDouble() * PauseJitter);
        }
    }
}
