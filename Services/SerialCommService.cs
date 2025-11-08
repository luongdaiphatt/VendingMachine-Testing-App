using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VendingMachineTest.Services
{
    public class COMService
    {
        private readonly SerialPort _serialPort;
        private readonly object _locker = new();
        private readonly int _retryCount = 5;
        private readonly int _ackTimeout = 300; // ms
        private readonly ManualResetEventSlim _ackReceived = new(false);
        private byte[]? _lastResponse;

        public event Action<string>? OnLog;
        public event Action<byte[]>? OnDataReceived;

        public COMService(string portName)
        {
            _serialPort = new SerialPort(portName, 57600, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = 500,
                WriteTimeout = 500
            };
            _serialPort.DataReceived += SerialPort_DataReceived;
        }

        public bool IsConnected => _serialPort?.IsOpen ?? false;

        public bool Connect()
        {
            try
            {
                if (!_serialPort.IsOpen)
                    _serialPort.Open();

                OnLog?.Invoke($"Kết nối COM thành công: {_serialPort.PortName}");
                return true;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"Không thể mở cổng COM: {ex.Message}");
                return false;
            }
        }

        public void Disconnect()
        {
            try
            {
                if (_serialPort.IsOpen)
                    _serialPort.Close();

                OnLog?.Invoke("Đã ngắt kết nối COM.");
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"Lỗi khi ngắt kết nối: {ex.Message}");
            }
        }

        /// <summary>
        /// Gửi command và chờ ACK từ VMC. Nếu không nhận được ACK sẽ retry tối đa 5 lần.
        /// </summary>
        public async Task<bool> SendCommandAsync(byte[] command)
        {
            if (!IsConnected)
            {
                OnLog?.Invoke("COM chưa được kết nối.");
                return false;
            }

            string hexCommand = BitConverter.ToString(command).Replace("-", " ");
            OnLog?.Invoke($"Gửi command: {hexCommand}");

            for (int attempt = 1; attempt <= _retryCount; attempt++)
            {
                try
                {
                    lock (_locker)
                    {
                        _serialPort.DiscardInBuffer();
                        _serialPort.DiscardOutBuffer();
                        _serialPort.Write(command, 0, command.Length);
                    }

                    OnLog?.Invoke($"[{attempt}/{_retryCount}] Đang chờ ACK...");

                    _ackReceived.Reset();
                    bool gotAck = await Task.Run(() => _ackReceived.Wait(_ackTimeout));

                    if (gotAck)
                    {
                        OnLog?.Invoke("Đã nhận ACK từ VMC.");
                        return true;
                    }
                    else
                    {
                        OnLog?.Invoke($"Không nhận ACK, thử lại lần {attempt + 1}...");
                    }
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"Lỗi khi gửi command: {ex.Message}");
                }

                await Task.Delay(200);
            }

            OnLog?.Invoke("Gửi thất bại sau 5 lần retry.");
            return false;
        }

        /// <summary>
        /// Sự kiện nhận dữ liệu từ cổng COM (ACK, DATA, NACK...)
        /// </summary>
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                Thread.Sleep(30); // đợi frame đầy đủ
                int length = _serialPort.BytesToRead;
                byte[] buffer = new byte[length];
                _serialPort.Read(buffer, 0, length);

                _lastResponse = buffer;
                OnDataReceived?.Invoke(buffer);

                string hex = BitConverter.ToString(buffer).Replace("-", " ");
                OnLog?.Invoke($"Nhận: {hex}");

                // Kiểm tra ACK (0x06)
                if (buffer.Length > 0 && buffer[0] == 0x06)
                {
                    _ackReceived.Set();
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"Lỗi khi đọc dữ liệu: {ex.Message}");
            }
        }

        public byte[]? GetLastResponse() => _lastResponse;
    }
}
