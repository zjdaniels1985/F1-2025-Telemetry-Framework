using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using F1TelemetryDashboard.Models;
using F1TelemetryDashboard.Parsers;
using F1TelemetryDashboard.Services;
using F1TelemetryDashboard.Transport;

namespace F1TelemetryDashboard
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private const int MaxCars = 22;
        private UdpTelemetryListener? _listener;
        private readonly F12025TelemetryParser _parser = new();
        private readonly Dictionary<int, LapTimeEntry> _allFastestLaps = new();

        private string _bindAddress = "127.0.0.1";
        private string _port = "2077";
        private string _connectionStatus = "Not connected";

        public string BindAddress
        {
            get => _bindAddress;
            set { _bindAddress = value; OnPropertyChanged(nameof(BindAddress)); }
        }

        public string Port
        {
            get => _port;
            set { _port = value; OnPropertyChanged(nameof(Port)); }
        }

        public string ConnectionStatus
        {
            get => _connectionStatus;
            set { _connectionStatus = value; OnPropertyChanged(nameof(ConnectionStatus)); }
        }

        public ObservableCollection<RacePositionRow> RaceOrder { get; } = new();
        public ObservableCollection<LapTimeEntry> FastestLaps { get; } = new();
        public ObservableCollection<CarVisual> CarVisuals { get; } = new();

        public ICommand ReconnectCommand { get; }

        public MainViewModel()
        {
            ReconnectCommand = new RelayCommand(OnReconnect);
            InitializeCollections();
            OnReconnect(null);
        }

        private void InitializeCollections()
        {
            for (int i = 0; i < MaxCars; i++)
            {
                RaceOrder.Add(new RacePositionRow { CarIndex = i, Position = 0 });
                CarVisuals.Add(new CarVisual { CarIndex = i });
            }
        }

        private void OnReconnect(object? parameter)
        {
            _listener?.Stop();
            _listener?.Dispose();
            _listener = null;

            if (!int.TryParse(Port, out int port) || port <= 0 || port > 65535)
            {
                ConnectionStatus = "Invalid port";
                return;
            }

            try
            {
                _listener = new UdpTelemetryListener(BindAddress, port);
                _listener.PacketReceived += OnPacketReceived;
                _listener.Start();
                ConnectionStatus = $"Listening on {BindAddress}:{port}";
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"Error: {ex.Message}";
            }
        }

        private void OnPacketReceived(byte[] buffer, System.Net.IPEndPoint remote)
        {
            var parsed = _parser.ParsePacket(buffer);
            if (parsed == null) return;

            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (parsed.Positions != null)
                {
                    UpdateRaceOrder(parsed.Positions);
                }

                if (parsed.WorldPositions != null)
                {
                    UpdateTrackMap(parsed.WorldPositions);
                }

                if (parsed.LapTime != null)
                {
                    UpdateFastestLaps(parsed.LapTime);
                }
            });
        }

        private void UpdateRaceOrder(List<RacePositionSnapshot> positions)
        {
            foreach (var pos in positions)
            {
                if (pos.CarIndex < 0 || pos.CarIndex >= RaceOrder.Count) continue;
                var row = RaceOrder[pos.CarIndex];
                row.Position = pos.Position;
                row.DriverShort = pos.DriverShort;
                row.Lap = pos.Lap;
                row.LastLapTimeMs = pos.LastLapTimeMs;
                row.BestLapTimeMs = pos.BestLapTimeMs;
                row.GapToLeaderMs = pos.GapToLeaderMs;
            }

            // Sort by position
            var sorted = RaceOrder.OrderBy(r => r.Position == 0 ? int.MaxValue : r.Position).ToList();
            RaceOrder.Clear();
            foreach (var item in sorted)
            {
                RaceOrder.Add(item);
            }
        }

        private void UpdateTrackMap(List<CarWorldPosition> worldPositions)
        {
            // Compute bounds
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            foreach (var wp in worldPositions)
            {
                if (wp.WorldX < minX) minX = wp.WorldX;
                if (wp.WorldX > maxX) maxX = wp.WorldX;
                if (wp.WorldZ < minZ) minZ = wp.WorldZ;
                if (wp.WorldZ > maxZ) maxZ = wp.WorldZ;
            }

            float rangeX = maxX - minX;
            float rangeZ = maxZ - minZ;
            if (rangeX < 1) rangeX = 1;
            if (rangeZ < 1) rangeZ = 1;

            const double canvasWidth = 1200;
            const double canvasHeight = 600;
            const double margin = 50;

            foreach (var wp in worldPositions)
            {
                if (wp.CarIndex < 0 || wp.CarIndex >= CarVisuals.Count) continue;
                var visual = CarVisuals[wp.CarIndex];

                // Normalize to 0..1
                double normX = (wp.WorldX - minX) / rangeX;
                double normZ = (wp.WorldZ - minZ) / rangeZ;

                visual.CanvasX = margin + normX * (canvasWidth - 2 * margin);
                visual.CanvasY = margin + normZ * (canvasHeight - 2 * margin);
                visual.Label = wp.DriverShort;
                visual.DotBrush = new SolidColorBrush(Color.FromArgb(
                    (byte)((wp.TeamColorArgb >> 24) & 0xFF),
                    (byte)((wp.TeamColorArgb >> 16) & 0xFF),
                    (byte)((wp.TeamColorArgb >> 8) & 0xFF),
                    (byte)(wp.TeamColorArgb & 0xFF)));
            }
        }

        private void UpdateFastestLaps(LapTimeEvent lapTime)
        {
            int key = lapTime.CarIndex * 1000 + lapTime.LapNumber;
            if (!_allFastestLaps.ContainsKey(key))
            {
                _allFastestLaps[key] = new LapTimeEntry
                {
                    CarIndex = lapTime.CarIndex,
                    DriverShort = lapTime.DriverShort,
                    LapNumber = lapTime.LapNumber,
                    LapTimeMs = lapTime.LapTimeMs
                };
            }

            // Top 5 fastest
            var top5 = _allFastestLaps.Values.OrderBy(l => l.LapTimeMs).Take(5).ToList();
            FastestLaps.Clear();
            int rank = 1;
            foreach (var lap in top5)
            {
                lap.Rank = rank++;
                FastestLaps.Add(lap);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => _execute(parameter);
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
