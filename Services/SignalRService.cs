using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace VendingMachineTest
{
    public class SignalRService : ICommService
    {
        private readonly string _url;
        private HubConnection _hub;

        public event Action<byte[]> DataReceived;
        public event Action<string> Log;
        public bool IsConnected => _hub?.State == HubConnectionState.Connected;

        public SignalRService(string url)
        {
            _url = url;
            Log?.Invoke($"SignalR Service initialized with URL: {_url}");
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                Log?.Invoke($"Attempting to connect to SignalR: {_url}");

                _hub = new HubConnectionBuilder()
                    .WithUrl(_url)
                    .WithAutomaticReconnect(new[]
                    {
                        TimeSpan.Zero,
                        TimeSpan.FromSeconds(2),
                        TimeSpan.FromSeconds(5),
                        TimeSpan.FromSeconds(10)
                    })
                    .Build();

                // ---- Connection events ----
                _hub.Reconnecting += error =>
                {
                    Log?.Invoke($"SignalR reconnecting... {error?.Message}");
                    return Task.CompletedTask;
                };

                _hub.Reconnected += connectionId =>
                {
                    Log?.Invoke($"SignalR reconnected! ConnectionId: {connectionId}");
                    return Task.CompletedTask;
                };

                _hub.Closed += error =>
                {
                    Log?.Invoke($"SignalR connection closed. {error?.Message}");
                    return Task.CompletedTask;
                };

                // ---- Register message handler (using string) ----
                _hub.On<string>("ReceiveFromVMC", (hexString) =>
                {
                    try
                    {
                        byte[] bytes = ParseHexString(hexString);
                        string hex = BitConverter.ToString(bytes);
                        Log?.Invoke($"[VMC] {hex}");
                        DataReceived?.Invoke(bytes);
                    }
                    catch (Exception ex)
                    {
                        Log?.Invoke($"[RX] Invalid HEX data: {ex.Message}");
                    }
                });

                Log?.Invoke($"Starting SignalR connection...");
                await _hub.StartAsync();
                Log?.Invoke($"SignalR connected successfully!");

                return true;
            }
            catch (HttpRequestException httpEx)
            {
                Log?.Invoke($"Network error: Cannot reach server at {_url}");
                Log?.Invoke($"Details: {httpEx.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Log?.Invoke($"SignalR connection failed!");
                Log?.Invoke($"Error: {ex.GetType().Name} - {ex.Message}");
                if (ex.InnerException != null)
                    Log?.Invoke($"Inner Error: {ex.InnerException.Message}");
                return false;
            }
        }

        private byte[] ParseHexString(string hexString)
        {
            if (string.IsNullOrWhiteSpace(hexString))
                return Array.Empty<byte>();

            return hexString
                .Split(new[] { ' ', '-', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => Convert.ToByte(s.Replace("0x", "").Trim(), 16))
                .ToArray();
        }

        public async Task DisconnectAsync()
        {
            try
            {
                if (_hub != null)
                {
                    Log?.Invoke($"Disconnecting from SignalR...");

                    if (_hub.State == HubConnectionState.Connected)
                    {
                        await _hub.StopAsync();
                        Log?.Invoke($"SignalR disconnected successfully");
                    }
                    else
                    {
                        Log?.Invoke($"SignalR already disconnected (State: {_hub.State})");
                    }

                    await _hub.DisposeAsync();
                    _hub = null;
                }
            }
            catch (Exception ex)
            {
                Log?.Invoke($"Error during disconnect: {ex.Message}");
            }
        }

        public async Task SendAsync(byte[] data)
        {
            if (_hub?.State != HubConnectionState.Connected)
            {
                Log?.Invoke($"Cannot send: Not connected (State: {_hub?.State})");
                return;
            }

            try
            {
                // Convert bytes -> hex string format: "0xA1 0xB2 0xC3"
                string hexString = string.Join(" ", data.Select(b => $"0x{b:X2}"));
                string logHex = BitConverter.ToString(data);

                await _hub.InvokeAsync("SendCommand", hexString);

                Log?.Invoke($"[PC] {logHex}");
            }
            catch (Exception ex)
            {
                Log?.Invoke($"Send failed: {ex.GetType().Name}");
                Log?.Invoke($"Error: {ex.Message}");
            }
        }      
    }
}
