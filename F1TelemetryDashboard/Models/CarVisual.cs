using System.Windows.Media;

namespace F1TelemetryDashboard.Models
{
    public class CarVisual
    {
        public int CarIndex { get; set; }
        public string Label { get; set; } = "";
        public double CanvasX { get; set; }
        public double CanvasY { get; set; }
        public Brush DotBrush { get; set; } = Brushes.Red;

        public double WorldX { get; set; }
        public double WorldZ { get; set; }
    }
}
