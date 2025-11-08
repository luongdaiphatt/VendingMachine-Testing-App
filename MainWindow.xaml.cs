using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Security.Policy;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using VendingMachineTest.Models;
using VendingMachineTest.Services;

namespace VendingMachineTest
{
    public partial class MainWindow : Window
    {
        private  ComPortService _portService;
        private SignalRService _signalRService;
        private VmcCommHandler _vmcHandler;
        private SerialPort _serialPort;
        private readonly ObservableCollection<OperationLog> _operationLogs = new();

        public MainWindow()
        {
            InitializeComponent();
            InitializeUI();
            LoadSerialPorts();
            Closing += MainWindow_Closing;
            GridOperationLog.ItemsSource = _operationLogs;
        }
        private void LoadSerialPorts()
        {
            var ports = SerialPort.GetPortNames().OrderBy(p => p).ToArray();
            ComboPorts.ItemsSource = ports;
            if (ports.Length > 0) ComboPorts.SelectedIndex = 0;
        }
        #region --- UI INITIALIZATION ---
        private void InitializeUI()
        {
            var ports = SerialPort.GetPortNames();
            ComboPorts.ItemsSource = ports;
            if (ports.Length > 0) ComboPorts.SelectedIndex = 0;
           
            tbUrl.Text = "http://localhost:5244/vmcHub";

            BtnConnect.Click += BtnConnect_Click;
            BtnDisconnect.Click += BtnDisconnect_Click;
            BtnSend.Click += BtnSend_Click;

            BtnConnectUrl.Click += BtnConnectUrl_Click;
            BtnDisconnectUrl.Click += BtnDisconnectUrl_Click;
            BtnSendd.Click += BtnSendSequential_Click;
        }
        #endregion

        #region --- SERIAL CONNECTION ---
        public void BoardBaudRate()
        {
            if (_serialPort == null) return;
            if (_serialPort.IsOpen)
            {
                _serialPort.Close();
                _serialPort.BaudRate = 57600;
                _serialPort.Open();
            }
            else
            {
                _serialPort.BaudRate = 57600;
                _serialPort.Open();
            }
        }
        private async void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            var portName = ComboPorts.SelectedItem as string;
            if (string.IsNullOrEmpty(portName))
            {
                MessageBox.Show("Please select COM port first.");
                return;
            }

            _portService = new ComPortService(portName, 57600);
            _portService.Log += AddLog;
            _portService.DataReceived += OnComDataReceived;

            bool ok = await _portService.ConnectAsync();
            if (ok)
                SetComConnectionStatus(true);
        }

        private async void BtnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    _serialPort.Close();
                    _serialPort.Dispose();
                }

                AddLog("Disconnected.");
                SetComConnectionStatus(false);
            }
            catch (Exception ex)
            {
                AddLog("Disconnect error: " + ex.Message);
            }
        }

        private void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            if (_vmcHandler == null) return;

            var items = ParseMultiInput(MachineID.Text);
            if (items.Count == 0)
            {
                AddLog("Invalid input format");
                return;
            }

            foreach (var (row, col, qty) in items)
            {
                for (int i = 0; i < qty; i++)
                {
                    byte[] data = { row, col };
                    _vmcHandler.QueueCommand(VmcProtocol.CMD_TYPE_01, data);
                }
            }

            AddLog($"Added {items.Count} into queue VMC");
        }
        #endregion

        #region --- SIGNALR CONNECTION ---
        private async void BtnConnectUrl_Click(object sender, RoutedEventArgs e)
        {
            if (tbUrl.Text == null)
            {
                MessageBox.Show("Please enter server URL", "Warning");
                return;
            }
            SetConnectionStatus(true);
            _signalRService = new SignalRService(tbUrl.Text.ToString());
            _signalRService.Log += OnLog;

            if (await _signalRService.ConnectAsync())
            {
                _vmcHandler = new VmcCommHandler(_signalRService, this);
                _vmcHandler.Log += OnLog;

                AddLog("SignalR connected & VMC simulator started.");
            }
        }

        private async void BtnDisconnectUrl_Click(object sender, RoutedEventArgs e)
        {
            if (_signalRService == null) return;
            SetConnectionStatus(false);
            await _signalRService.DisconnectAsync();
            _signalRService.Log -= OnLog;
            _signalRService = null;
            
        }
        #endregion

        #region --- BATCH COMMAND SENDING ---
        private void BtnSendSequential_Click(object sender, RoutedEventArgs e)
        {
            if (_vmcHandler == null) return;

            var items = ParseMultiInput(MachineIDD.Text);
            if (items.Count == 0)
            {
                AddLog("Wrong format");
                return;
            }

            int totalCommands = 0;
            foreach (var (row, col, qty) in items)
            {
                for (int q = 0; q < qty; q++)
                {
                    byte[] data = { row, col };
                    _vmcHandler.QueueCommand(VmcProtocol.CMD_TYPE_01, data);
                    totalCommands++;
                }
            }
        }
        #endregion

        #region --- UTILITIES ---
        private List<(byte row, byte column, int quantity)> ParseMultiInput(string input)
        {
            var results = new List<(byte, byte, int)>();
            if (string.IsNullOrWhiteSpace(input)) return results;

            var groups = input.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var group in groups)
            {
                var parts = group.Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                if (byte.TryParse(parts[0].Trim(), out byte row) &&
                    byte.TryParse(parts[1].Trim(), out byte col))
                {
                    int qty = 1;
                    if (parts.Length >= 3 && int.TryParse(parts[2].Trim(), out int q))
                        qty = q;
                    results.Add((row, col, qty));
                }
            }
            return results;
        }

        private void OnLog(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                TxtLog.AppendText($"[{timestamp}] {msg}\n");
                TxtLog.ScrollToEnd();
            });
        }

        private void AddLog(string msg) => OnLog(msg);

        public void AddOperationLog(string channelId, string checkStatus, string releaseStatus)
        {
            Dispatcher.Invoke(() =>
            {
                _operationLogs.Add(new OperationLog
                {
                    Time = DateTime.Now.ToString("HH:mm:ss"),
                    ChannelID = channelId,
                    CheckStatus = checkStatus,
                    ReleaseStatus = releaseStatus
                });

                if (_operationLogs.Count > 100)
                    _operationLogs.RemoveAt(0);
            });
        }

        private async void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_portService?.IsConnected == true)
                _portService.DisconnectAsync();

            if (_signalRService?.IsConnected == true)
                await _signalRService.DisconnectAsync();
        }
        private void OnComDataReceived(byte[] data)
        {
            Dispatcher.Invoke(() =>
            {
                AddLog($"RX: {BitConverter.ToString(data)}");
            });
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            _operationLogs.Clear();
        }
        #endregion

        #region UI Helpers
        public void SetConnectionStatus(bool isConnected)
        {
            Dispatcher.Invoke(() =>
            {
                BtnConnect.IsEnabled = !isConnected;
                BtnDisconnect.IsEnabled = !isConnected;
                BtnConnectUrl.IsEnabled = !isConnected;
                BtnDisconnectUrl.IsEnabled = isConnected;
                BtnSend.IsEnabled = !isConnected;
                BtnSendd.IsEnabled = isConnected;
            });
        }
        public void SetComConnectionStatus(bool isConnected)
        {
            Dispatcher.Invoke(() =>
            {
                BtnConnect.IsEnabled = !isConnected;
                BtnDisconnect.IsEnabled = isConnected;
                BtnConnectUrl.IsEnabled = !isConnected;
                BtnDisconnectUrl.IsEnabled = !isConnected;
                BtnSend.IsEnabled = isConnected;
                BtnSendd.IsEnabled = !isConnected;
            });
        }
        #endregion
    }
}