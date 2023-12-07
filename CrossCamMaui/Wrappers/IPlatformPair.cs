using ErrorEventArgs = CrossCam.CustomElement.ErrorEventArgs;

namespace CrossCam.Wrappers
{
    public interface IPlatformPair
    {
        void Disconnect();
        Task StartScanning();
        Task BecomeDiscoverable();
        event EventHandler Connected;
        event EventHandler Disconnected;

        void SendPayload(byte[] bytes);
        event EventHandler<byte[]> PayloadReceived;
        event EventHandler<ErrorEventArgs> ErrorOccurred;
    }
}