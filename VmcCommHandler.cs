using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using VendingMachineTest.Services;

namespace VendingMachineTest
{
    public class VmcCommHandler
    {
        private readonly ICommService _commService;
        private readonly object _lock = new();
        private readonly ConcurrentQueue<VmcProtocol.Packet> _commandQueue = new();

        private byte _packNo = 1;
        private VmcProtocol.Packet _currentCommand;
        private bool _waitingAckFromVmc;
        private bool _waitingDataFromVmc;

        private readonly MainWindow _mainWindow;

        public event Action<string> Log;
        public event Action<VmcProtocol.Packet> PacketReceived;
        public event Action<VmcProtocol.Packet> PollReceived;

        public VmcCommHandler(ICommService commService, MainWindow mainWindow)
        {
            _commService = commService;
            _commService.DataReceived += OnDataReceived;
            _mainWindow = mainWindow;
        }

        private async void OnDataReceived(byte[] raw)
        {
            try
            {
                if (raw == null || raw.Length == 0)
                {
                    Log?.Invoke("[VMC] RX Empty data");
                    return;
                }

                var packet = VmcProtocol.ParsePacket(raw);
                if (packet == null)
                {
                    Log?.Invoke($"[VMC] RX Invalid packet: {BitConverter.ToString(raw)}");
                    return;
                }

                PacketReceived?.Invoke(packet);
                await HandlePacket(packet);
            }
            catch (Exception ex)
            {
                Log?.Invoke($"Error processing received data: {ex.Message}");
            }
        }

        private async Task HandlePacket(VmcProtocol.Packet packet)
        {
            switch (packet.Command)
            {
                case VmcProtocol.CMD_POLL:
                    PollReceived?.Invoke(packet);
                    await HandlePoll();
                    break;

                case VmcProtocol.CMD_ACK:
                    HandleAckFromVmc();
                    break;

                case VmcProtocol.CMD_TYPE_02:
                    await HandleChannelStatus(packet);
                    break;

                case VmcProtocol.CMD_TYPE_04:
                    await HandleDispenseReport(packet);
                    break;

                default:
                    Log?.Invoke($"Received unknown command 0x{packet.Command:X2}");
                    await SendAck();
                    break;
            }
        }

        private async Task HandlePoll()
        {
            VmcProtocol.Packet commandToSend = null;
            bool shouldSendCommand = false;

            lock (_lock)
            {
                if (_waitingAckFromVmc || _waitingDataFromVmc)
                {
                    
                }
                else
                {
                    if (_commandQueue.TryPeek(out var next))
                    {
                        _currentCommand = next;
                        commandToSend = next;
                        _waitingAckFromVmc = true;
                        shouldSendCommand = true;
                    }
                }
            }

            if (shouldSendCommand && commandToSend != null)
            {
                await SendPacket(commandToSend);
                Log?.Invoke($"Sent CMD 0x{commandToSend.Command:X2} (PackNO:{commandToSend.PackNO})");
            }
            else
            {
                await SendAck();
            }
        }

        private void HandleAckFromVmc()
        {
            lock (_lock)
            {
                if (_waitingAckFromVmc && _currentCommand != null)
                {
                    _waitingAckFromVmc = false;
                    _waitingDataFromVmc = true;
                    Log?.Invoke("ACK received from VMC");
                }
                else
                {
                    Log?.Invoke("Unexpected ACK (no command waiting)");
                }
            }
        }

        private async Task HandleChannelStatus(VmcProtocol.Packet packet)
        {
            if (packet.Text == null || packet.Text.Length < 3)
            {
                Log?.Invoke("CMD_TYPE_02: Invalid data");
                await SendAck();
                CompleteCurrentCommand();
                return;
            }

            byte status = packet.Text[0];
            byte row = packet.Text[1];
            byte col = packet.Text[2];

            string statusText = status switch
            {
                0x01 => "Bình thường",
                0x02 => "Hết hàng",
                0x03 => "Không tồn tại",
                0x04 => "Tạm dừng",
                _ => $"Không xác định (0x{status:X2})"
            };

            Log?.Invoke($"CMD_TYPE_02: Row={row}, Col={col} - {statusText}");
            _mainWindow.AddOperationLog($"Row {row}, Col {col}", statusText, "-");

            await SendAck();

            if (status == 0x01)
            {
                Log?.Invoke($"Send CMD_TYPE_06");

                byte currentPackNo;
                lock (_lock)
                {
                    currentPackNo = _currentCommand?.PackNO ?? _packNo;

                    _commandQueue.TryDequeue(out _);
                    _currentCommand = null;
                    _waitingAckFromVmc = false;
                    _waitingDataFromVmc = false;
                }
                byte[] confirmData = { 0x01, 0x00, row, col };
                var packet06 = VmcProtocol.CreateCommandPacket(VmcProtocol.CMD_TYPE_06, currentPackNo, confirmData);
                _commandQueue.Enqueue(packet06);

                Log?.Invoke($"Queued CMD_TYPE_06 (PackNO:{currentPackNo}) Data:{BitConverter.ToString(confirmData)}");
                return;
            }

            Log?.Invoke($"Error (status=0x{status:X2})");
            CompleteCurrentCommand();
        }

        private async Task HandleDispenseReport(VmcProtocol.Packet packet)
        {
            if (packet.Text == null || packet.Text.Length < 3)
            {
                Log?.Invoke("CMD_TYPE_04: Invalid data");
                await SendAck();
                //CompleteCurrentCommand();
                return;
            }

            byte status = packet.Text[0];
            byte row = packet.Text[1];
            byte col = packet.Text[2];

            string statusText = status switch
            {
                0x01 => "Đang xuất hàng",
                0x02 => "Xuất hàng thành công",
                0x03 => "Kẹt hàng",
                0x04 => "Động cơ không dừng đúng cách",
                0x06 => "Không có động cơ",
                _ => $"Trạng thái khác (0x{status:X2})"
            };

            Log?.Invoke($"CMD_TYPE_04: Row={row}, Col={col} - {statusText}");
            _mainWindow.AddOperationLog($"Row {row}, Col {col}", "-", statusText);
            if(status == 0x01)
            {
                Log?.Invoke($"CMD_TYPE_04: Đang xuất hàng ...");
                await SendAck();
                return;
            }  
            else
            {
                await SendAck();
                CompleteCurrentCommand();
                Log?.Invoke($"Successful");
                return;
            }
        }

        private void CompleteCurrentCommand()
        {
            lock (_lock)
            {
                _commandQueue.TryDequeue(out _);
                _currentCommand = null;
                _waitingAckFromVmc = false;
                _waitingDataFromVmc = false;
                _packNo++;
                if (_packNo == 0) _packNo = 1;
            }
        }

        public void QueueCommand(byte commandType, byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                Log?.Invoke("QueueCommand called with empty data");
                return;
            }

            byte currentPackNo;
            lock (_lock)
            {
                currentPackNo = _packNo;
            }

            var packet = VmcProtocol.CreateCommandPacket(commandType, currentPackNo, data);
            _commandQueue.Enqueue(packet);

            Log?.Invoke($"Queued CMD 0x{commandType:X2} (PackNO:{currentPackNo}) Data:{BitConverter.ToString(data)}");
        }

        private async Task SendAck()
        {
            await SendPacket(VmcProtocol.CreateAck());
        }

        public async Task SendPacket(VmcProtocol.Packet packet)
        {
            if (!_commService.IsConnected)
            {
                Log?.Invoke("Cannot send: not connected");
                return;
            }

            byte[] bytes = packet.ToBytes();
            await _commService.SendAsync(bytes);
        }
    }
}