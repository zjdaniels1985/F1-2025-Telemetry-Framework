using F1TelemetryDashboard.Transport;

namespace F1TelemetryDashboard.Services
{
    public interface ITelemetryParser
    {
        ParsedPacket? ParsePacket(byte[] buffer);
    }
}
