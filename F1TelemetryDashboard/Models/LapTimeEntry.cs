using System;

namespace F1TelemetryDashboard.Models
{
    public class LapTimeEntry
    {
        public int Rank { get; set; }
        public int CarIndex { get; set; }
        public string DriverShort { get; set; } = "";
        public int LapNumber { get; set; }
        public int LapTimeMs { get; set; }
        public string TimeDisplay
        {
            get
            {
                var ts = TimeSpan.FromMilliseconds(LapTimeMs);
                return $"{(int)ts.TotalMinutes}:{ts.Seconds:00}.{ts.Milliseconds/10:00}";
            }
        }
    }
}
