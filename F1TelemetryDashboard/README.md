# F1 2025 Telemetry Dashboard (WPF, .NET 8)

Windows 10/11 WPF dashboard for EA Sports F1 25 UDP telemetry.

- Top: Race order (position, driver short code, lap, last/best lap, gap)
- Middle: Fastest 5 laps (uses FTLP event and updates in real-time)
- Bottom: Top‑down track map using world X/Z from Motion data

## Telemetry Setup

- In-game, enable UDP telemetry and set the target IP to your PC and the port to match the app.
- Default port here is `2077` and default bind IP is `127.0.0.1` (you can change both in the toolbar at runtime).
- The app binds to the IP you specify (e.g., `127.0.0.1` for local only, `0.0.0.0` for all interfaces) and listens for broadcast/unicast.

Allow the app (or the port) through Windows Firewall if needed.

## Build

- .NET 8 SDK on Windows
- Open the solution folder in Visual Studio 2022 or run `dotnet build` inside `F1TelemetryDashboard`.

## Implementation Notes

- Parser uses the 2025 header and these packet types:
  - Motion (Id 0) for world X/Z per car (track map).
  - LapData (Id 2) for race position, lap number, last-lap time, and delta to leader.
  - Event (Id 3) for fastest-lap events ("FTLP").
  - Participants (Id 4) for driver names and team colours (used to color dots and generate 3-letter codes).
- Best lap per car is tracked locally and displayed in the race order table.
- Driver short label is derived from the participant name (e.g., Hamilton -> HAM). If names are absent, falls back to `CAR <idx>`.

## Extensibility

- You can add additional packet support (car telemetry, status, etc.) by expanding `F12025TelemetryParser`.
- For more accurate team colours, map `teamId` to an explicit palette if desired.
