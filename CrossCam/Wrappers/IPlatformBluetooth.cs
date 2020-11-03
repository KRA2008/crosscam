using System;
using System.Threading.Tasks;

namespace CrossCam.Wrappers
{
    public interface IPlatformBluetooth
    {
        void Disconnect();
        Task<bool> RequestBluetoothPermissions();
        Task<bool> RequestLocationPermissions();
        Task<bool> TurnOnLocationServices();
        Task<bool> StartScanning();
        event EventHandler<PartnerDevice> DeviceDiscovered;
        Task<bool> BecomeDiscoverable();
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