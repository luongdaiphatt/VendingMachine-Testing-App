using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VendingMachineTest.Services
{
    public class ComPortService : ICommService, IDisposable
    {
        private SerialPort _serialPort;
        private CancellationTokenSource _cts;
        private readonly object _lock = new();

        private readonly string _portName;
        private readonly int _baudRate;

        public event Action<byte[]> DataReceived;
        public event Action<string> Log;

        public bool IsConnected => _serialPort != null && _serialPort.IsOpen;

        public ComPortService(string portName, int baudRate = 57600)
        {
            _portName = portName;
            _baudRate = baudRate;
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                if (IsConnected)
                    await DisconnectAsync();

                _serialPort = new SerialPort(_portName, _baudRate, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = 500,
                    WriteTimeout = 500
                };

                _serialPort.Open();
                _cts = new CancellationTokenSource();

                _ = Task.Run(() => ReadLoopAsync(_cts.Token));

                Log?.Invoke($"[COM] Connected {_portName} @ {_baudRate}");
                return true;
            }
            catch (Exception ex)
            {
                Log?.Invoke($"[COM] Connect failed: {ex.Message}");
                return false;
            }
        }

        private async Task ReadLoopAsync(CancellationToken token)
        {
            byte[] buffer = new byte[256];

            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_serialPort == null || !_serialPort.IsOpen)
                    {
                        await Task.Delay(100, token);
                        continue;
                    }

                    int bytesToRead = _serialPort.BytesToRead;
                    if (bytesToRead > 0)
                    {
                        int len = _serialPort.Read(buffer, 0, Math.Min(bytesToRead, buffer.Length));
                        if (len > 0)
                        {
                            byte[] data = new byte[len];
                            Array.Copy(buffer, 0, data, 0, len);

                            Log?.Invoke($"[COM RX] {BitConverter.ToString(data)}");
                            DataReceived?.Invoke(data);
                        }
                    }

                    await Task.Delay(10, token); // tránh CPU 100%
                }
                catch (TimeoutException)
                {
                    // không sao, bỏ qua
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log?.Invoke($"[COM] Read error: {ex.Message}");
                    await Task.Delay(200);
                }
            }
        }

        public async Task SendAsync(byte[] data)
        {
            if (data == null || data.Length == 0) return;

            try
            {
                lock (_lock)
                {
                    if (_serialPort != null && _serialPort.IsOpen)
                    {
                        _serialPort.Write(data, 0, data.Length);
                    }
                    else
                    {
                        throw new InvalidOperationException("SerialPort is not open");
                    }
                }

                Log?.Invoke($"[COM TX] {BitConverter.ToString(data)}");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Log?.Invoke($"[COM] Send error: {ex.Message}");
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                _cts?.Cancel();

                if (_serialPort != null)
                {
                    if (_serialPort.IsOpen)
                        _serialPort.Close();

                    _serialPort.Dispose();
                    _serialPort = null;
                }

                Log?.Invoke("[COM] Disconnected.");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Log?.Invoke($"[COM] Disconnect error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _ = DisconnectAsync();
            _cts?.Dispose();
        }
    }
}
