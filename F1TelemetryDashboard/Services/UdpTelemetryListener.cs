using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace F1TelemetryDashboard.Services
{
    public sealed class UdpTelemetryListener : IDisposable
    {
        private readonly IPEndPoint _bindEndPoint;
        private readonly bool _allowBroadcast;
        private UdpClient? _client;
        private CancellationTokenSource? _cts;

        public delegate void PacketReceivedHandler(byte[] buffer, IPEndPoint remote);
        public event PacketReceivedHandler? PacketReceived;

        public UdpTelemetryListener(string bindAddress, int port, bool allowBroadcast = true)
        {
            _bindEndPoint = new IPEndPoint(IPAddress.Parse(bindAddress), port);
            _allowBroadcast = allowBroadcast;
        }

        public void Start()
        {
            if (_client != null) return;
            _cts = new CancellationTokenSource();
            _client = new UdpClient(AddressFamily.InterNetwork);
            _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            if (_allowBroadcast)
            {
                _client.EnableBroadcast = true;
            }
            _client.Client.Bind(_bindEndPoint);

            Task.Run(async () =>
            {
                while (!_cts!.IsCancellationRequested)
                {
                    try
                    {
                        var result = await _client.ReceiveAsync(_cts.Token);
                        PacketReceived?.Invoke(result.Buffer, result.RemoteEndPoint);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch
                    {
                        // Keep listening on transient errors
                    }
                }
            }, _cts.Token);
        }

        public void Stop()
        {
            _cts?.Cancel();
            _client?.Close();
            _client?.Dispose();
            _client = null;
            _cts = null;
        }

        public void Dispose() => Stop();
    }
}
