using System;
using System.Threading.Tasks;
using CrossCam.CustomElement;

namespace CrossCam.Wrappers
{
    public interface IPlatformBluetooth
    {
        BluetoothOperator BluetoothOperator { set; }
        void Disconnect();
        Task<string> StartScanning();
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