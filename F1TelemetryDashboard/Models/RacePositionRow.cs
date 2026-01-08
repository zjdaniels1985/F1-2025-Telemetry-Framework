using System;

namespace F1TelemetryDashboard.Models
{
    public class RacePositionRow
    {
        public int CarIndex { get; set; }
        public int Position { get; set; }
        public string DriverShort { get; set; } = "";
        public int Lap { get; set; }
        public int? LastLapTimeMs { get; set; }
        public int? BestLapTimeMs { get; set; }
        public int? GapToLeaderMs { get; set; }

        public string LastLapTimeDisplay => FormatMs(LastLapTimeMs);
        public string BestLapTimeDisplay => FormatMs(BestLapTimeMs);
        public string GapToLeaderDisplay => GapToLeaderMs.HasValue ? $"+{FormatMs(GapToLeaderMs)}" : "";

        private static string FormatMs(int? ms)
        {
            if (!ms.HasValue) return "";
            var ts = TimeSpan.FromMilliseconds(ms.Value);
            return $"{(int)ts.TotalMinutes}:{ts.Seconds:00}.{ts.Milliseconds/10:00}";
        }
    }
}
