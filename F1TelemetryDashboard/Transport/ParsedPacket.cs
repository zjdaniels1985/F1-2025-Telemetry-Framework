using System.Collections.Generic;

namespace F1TelemetryDashboard.Transport
{
    public class ParsedPacket
    {
        public List<RacePositionSnapshot>? Positions { get; set; }
        public List<CarWorldPosition>? WorldPositions { get; set; }
        public LapTimeEvent? LapTime { get; set; }
    }

    public class RacePositionSnapshot
    {
        public int CarIndex { get; set; }
        public int Position { get; set; }
        public string DriverShort { get; set; } = "";
        public int Lap { get; set; }
        public int? LastLapTimeMs { get; set; }
        public int? BestLapTimeMs { get; set; }
        public int? GapToLeaderMs { get; set; }
    }

    public class CarWorldPosition
    {
        public int CarIndex { get; set; }
        public float WorldX { get; set; }
        public float WorldZ { get; set; }
        public string DriverShort { get; set; } = "";
        public uint TeamColorArgb { get; set; } = 0xFFFF0000;
    }

    public class LapTimeEvent
    {
        public int CarIndex { get; set; }
        public string DriverShort { get; set; } = "";
        public int LapNumber { get; set; }
        public int LapTimeMs { get; set; }
    }
}
