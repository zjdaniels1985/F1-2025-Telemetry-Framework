using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using F1TelemetryDashboard.Services;
using F1TelemetryDashboard.Transport;

namespace F1TelemetryDashboard.Parsers
{
    // Parser for EA Sports F1 25 UDP telemetry (2025 format).
    // Supports: Motion (0), LapData (2), Event (3), Participants (4).
    public sealed class F12025TelemetryParser : ITelemetryParser
    {
        private const int MaxCars = 22;

        // Enrichment from Participants
        private readonly string[] _driverNames = Enumerable.Repeat("", MaxCars).ToArray();
        private readonly string[] _driverShort = Enumerable.Repeat("", MaxCars).ToArray();
        private readonly uint[] _teamColorArgb = Enumerable.Repeat(0xFFCC3333u, MaxCars).ToArray();

        private readonly int[] _lastLapMs = new int[MaxCars];
        private readonly int[] _bestLapMs = Enumerable.Repeat(int.MaxValue, MaxCars).ToArray();
        private readonly int[] _lastSeenLapNum = new int[MaxCars];

        public ParsedPacket? ParsePacket(byte[] buffer)
        {
            if (buffer.Length < 29) return null; // header size per spec

            // PacketHeader fields: packetId is at offset 6
            byte packetId = buffer[6];
            return packetId switch
            {
                0 => ParseMotion(buffer),
                2 => ParseLapData(buffer),
                3 => ParseEvent(buffer),
                4 => ParseParticipants(buffer),
                _ => null
            };
        }

        private ParsedPacket? ParseMotion(byte[] buf)
        {
            // PacketMotionData = header (29) + 22 * CarMotionData (60 bytes)
            const int headerSize = 29;
            const int carStride = 60;
            if (buf.Length < headerSize + carStride * MaxCars) return null;

            var list = new List<CarWorldPosition>(MaxCars);
            for (int i = 0; i < MaxCars; i++)
            {
                int o = headerSize + i * carStride;
                float worldX = ReadFloatLE(buf, o + 0);
                // float worldY = ReadFloatLE(buf, o + 4);
                float worldZ = ReadFloatLE(buf, o + 8);
                list.Add(new CarWorldPosition
                {
                    CarIndex = i,
                    WorldX = worldX,
                    WorldZ = worldZ,
                    DriverShort = SafeShort(i),
                    TeamColorArgb = _teamColorArgb[i]
                });
            }
            return new ParsedPacket { WorldPositions = list };
        }

        private ParsedPacket? ParseLapData(byte[] buf)
        {
            // PacketLapData: header (29) + 22 * LapData (57 bytes) + 2 bytes (TT indices)
            const int headerSize = 29;
            const int lapDataSize = 57;
            if (buf.Length < headerSize + lapDataSize * MaxCars) return null;

            var positions = new List<RacePositionSnapshot>(MaxCars);
            for (int i = 0; i < MaxCars; i++)
            {
                int o = headerSize + i * lapDataSize;

                uint lastLapMs = ReadUIntLE(buf, o + 0);

                // delta to race leader
                ushort deltaLeaderMsPart = ReadUShortLE(buf, o + 17);
                byte deltaLeaderMin = buf[o + 19];

                byte carPos = buf[o + 32];
                byte currentLapNum = buf[o + 33];

                if (lastLapMs > 0 && _lastLapMs[i] != lastLapMs)
                {
                    if (lastLapMs < _bestLapMs[i]) _bestLapMs[i] = (int)lastLapMs;
                }
                _lastLapMs[i] = (int)lastLapMs;
                _lastSeenLapNum[i] = currentLapNum;

                int? gapLeaderMs = null;
                if (deltaLeaderMin != 0xFF) // defensive
                {
                    gapLeaderMs = deltaLeaderMin * 60_000 + deltaLeaderMsPart;
                }

                positions.Add(new RacePositionSnapshot
                {
                    CarIndex = i,
                    Position = carPos,
                    DriverShort = SafeShort(i),
                    Lap = currentLapNum,
                    LastLapTimeMs = lastLapMs == 0 ? null : (int)lastLapMs,
                    BestLapTimeMs = _bestLapMs[i] == int.MaxValue ? null : _bestLapMs[i],
                    GapToLeaderMs = gapLeaderMs
                });
            }

            return new ParsedPacket { Positions = positions };
        }

        private ParsedPacket? ParseEvent(byte[] buf)
        {
            const int headerSize = 29;
            if (buf.Length < headerSize + 4) return null;
            string code = Encoding.ASCII.GetString(buf, headerSize, 4);

            if (code == "FTLP")
            {
                if (buf.Length < headerSize + 4 + 1 + 4) return null;
                int vehicleIdx = buf[headerSize + 4];
                float lapTimeSec = ReadFloatLE(buf, headerSize + 5);
                int lapTimeMs = (int)Math.Round(lapTimeSec * 1000.0);

                if (vehicleIdx >= 0 && vehicleIdx < MaxCars)
                {
                    if (lapTimeMs < _bestLapMs[vehicleIdx]) _bestLapMs[vehicleIdx] = lapTimeMs;
                }

                return new ParsedPacket
                {
                    LapTime = new LapTimeEvent
                    {
                        CarIndex = vehicleIdx,
                        DriverShort = SafeShort(vehicleIdx),
                        LapNumber = Math.Max(1, _lastSeenLapNum[vehicleIdx]),
                        LapTimeMs = lapTimeMs
                    }
                };
            }

            return null;
        }

        private ParsedPacket? ParseParticipants(byte[] buf)
        {
            const int headerSize = 29;
            if (buf.Length < headerSize + 1) return null;
            int o = headerSize;
            byte numActive = buf[o++];
            const int nameLen = 32;
            const int participantStride = 57; // per spec

            if (buf.Length < o + participantStride * MaxCars) return null;

            for (int i = 0; i < MaxCars; i++)
            {
                int p = o + i * participantStride;

                // Skip first 7 single-byte fields
                p += 7;

                // name
                string name = DecodeUtf8NullTerminated(buf, p, nameLen);
                p += nameLen;

                // yourTelemetry, showOnlineNames
                p += 2;

                // techLevel (2), platform (1)
                p += 3;

                byte numColours = buf[p++];

                byte r = 200, g = 60, b = 60;
                if (numColours > 0)
                {
                    if (p + 3 <= buf.Length)
                    {
                        r = buf[p + 0];
                        g = buf[p + 1];
                        b = buf[p + 2];
                    }
                }
                // move past 4 colours (12 bytes)
                p += 12;

                uint argb = 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | b;

                _driverNames[i] = name;
                _driverShort[i] = ComputeShortName(name, i);
                _teamColorArgb[i] = argb;
            }

            return null;
        }

        private static string DecodeUtf8NullTerminated(byte[] buf, int offset, int max)
        {
            int len = 0;
            while (len < max && buf[offset + len] != 0) len++;
            return Encoding.UTF8.GetString(buf, offset, len).Trim();
        }

        private string SafeShort(int carIdx)
        {
            var s = _driverShort[carIdx];
            if (!string.IsNullOrWhiteSpace(s)) return s;
            var n = _driverNames[carIdx];
            if (!string.IsNullOrWhiteSpace(n)) return ComputeShortName(n, carIdx);
            return $"CAR {carIdx}";
        }

        private static string ComputeShortName(string name, int carIdx)
        {
            var trimmed = name.Trim();
            if (trimmed.Length == 0) return $"C{carIdx:D2}";
            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string source = parts.Length > 0 ? parts[^1] : trimmed;
            var letters = new string(source.Where(char.IsLetter).ToArray()).ToUpperInvariant();
            if (letters.Length >= 3) return letters.Substring(0, 3);
            var all = new string(trimmed.Where(char.IsLetter).ToArray()).ToUpperInvariant();
            if (all.Length >= 3) return all.Substring(0, 3);
            return (all + "XXX").Substring(0, 3);
        }

        private static float ReadFloatLE(byte[] b, int o) => BitConverter.ToSingle(b, o);
        private static ushort ReadUShortLE(byte[] b, int o) => BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(o, 2));
        private static uint ReadUIntLE(byte[] b, int o) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(o, 4));
    }
}
