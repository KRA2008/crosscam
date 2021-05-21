using System;
using System.Threading.Tasks;

namespace CrossCam.Wrappers
{
    public interface IPlatformBluetooth
    {
        void Disconnect();
        Task StartScanning();
        Task BecomeDiscoverable();
        event EventHandler Connected;
        event EventHandler Disconnected;

        void SendPayload(byte[] bytes);
        event EventHandler<byte[]> PayloadReceived;
    }
}