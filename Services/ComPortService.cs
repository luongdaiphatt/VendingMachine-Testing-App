using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace VendingMachineTest.Services
{
    public class ComPortService : ICommService, IDisposable
    {
        private SerialPort _serialPort;
        private readonly object _lock = new();

        // --- ACK logic ---
        private readonly int _retryCount = 5;
        private readonly int _ackTimeout = 300; // ms
        private readonly ManualResetEventSlim _ackReceived = new(false);
        private byte[]? _lastResponse;

        public event Action<byte[]> DataReceived;
        public event Action<string> Log;

        public bool IsConnected => _serialPort?.IsOpen ?? false;

        public ComPortService(string portName, int baudRate = 57600)
        {
            _serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = 500,
                WriteTimeout = 500
            };
            _serialPort.DataReceived += SerialPort_DataReceived;
        }

        public Task<bool> ConnectAsync()
        {
            try
            {
                if (!_serialPort.IsOpen)
                    _serialPort.Open();

                Log?.Invoke($"[COM] Connected {_serialPort.PortName} @ {_serialPort.BaudRate}");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Log?.Invoke($"[COM] Connect failed: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                if (_serialPort != null && _serialPort.IsOpen)
                    _serialPort.Close();

                Log?.Invoke("[COM] Disconnected.");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Log?.Invoke($"[COM] Disconnect error: {ex.Message}");
            }
        }
        public async Task SendAsync(byte[] data)
        {
            if (!IsConnected || data == null || data.Length == 0) return;

            try
            {
                lock (_lock)
                {
                    _serialPort.DiscardOutBuffer();
                    _serialPort.Write(data, 0, data.Length);
                }

                Log?.Invoke($"[COM TX] {BitConverter.ToString(data)}");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Log?.Invoke($"[COM] Send error: {ex.Message}");
            }
        }
        public async Task<bool> SendCommandAsync(byte[] command)
        {
            if (!IsConnected)
            {
                Log?.Invoke("[COM] COM chưa kết nối.");
                return false;
            }

            string hexCommand = BitConverter.ToString(command).Replace("-", " ");
            Log?.Invoke($"[COM TX] Sending command: {hexCommand}");

            for (int attempt = 1; attempt <= _retryCount; attempt++)
            {
                try
                {
                    lock (_lock)
                    {
                        _serialPort.DiscardInBuffer();
                        _serialPort.DiscardOutBuffer();
                        _serialPort.Write(command, 0, command.Length);
                    }

                    _ackReceived.Reset();
                    Log?.Invoke($"[{attempt}/{_retryCount}] Waiting for ACK...");
                    bool gotAck = await Task.Run(() => _ackReceived.Wait(_ackTimeout));

                    if (gotAck)
                    {
                        Log?.Invoke("ACK received.");
                        return true;
                    }
                    else
                    {
                        Log?.Invoke("No ACK, retrying...");
                    }
                }
                catch (Exception ex)
                {
                    Log?.Invoke($"Error sending command: {ex.Message}");
                }

                await Task.Delay(200);
            }

            Log?.Invoke("[COM] Send command failed after retries.");
            return false;
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                Thread.Sleep(30); 
                int length = _serialPort.BytesToRead;
                if (length == 0) return;

                byte[] buffer = new byte[length];
                _serialPort.Read(buffer, 0, length);

                _lastResponse = buffer;
                DataReceived?.Invoke(buffer);

                string hex = BitConverter.ToString(buffer).Replace("-", " ");
                Log?.Invoke($"[COM RX] {hex}");

                // Kiểm tra ACK (0x06)
                if (buffer.Length > 0 && buffer[0] == 0x06)
                    _ackReceived.Set();
            }
            catch (Exception ex)
            {
                Log?.Invoke($"[COM] DataReceived error: {ex.Message}");
            }
        }

        public byte[]? GetLastResponse() => _lastResponse;

        public void Dispose()
        {
            _serialPort?.Dispose();
        }
    }
}
