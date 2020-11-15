using System;
using System.Threading.Tasks;

namespace CrossCam.Wrappers
{
    public interface IPlatformBluetooth
    {
        void Disconnect();
        Task<string> StartScanning();
        Task<bool> BecomeDiscoverable();
        event EventHandler<PartnerDevice> DeviceDiscovered;
        event EventHandler Connected;
        event EventHandler Disconnected;

        void SendPayload(byte[] bytes);
        event EventHandler<byte[]> PayloadReceived;
    }

    public class PartnerDevice
    {
        public string Name { get; set; }
        public string Address { get; set; }
    }
}