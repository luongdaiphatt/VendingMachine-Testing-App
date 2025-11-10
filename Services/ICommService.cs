public interface ICommService
{
    event Action<byte[]> DataReceived;
    event Action<string> Log;
    Task<bool> ConnectAsync();
    Task DisconnectAsync();
    Task SendAsync(byte[] data);
    bool IsConnected { get; }
}
