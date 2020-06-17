using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CrossCam.Wrappers
{
    public interface IPlatformBluetooth
    {
        void Disconnect();
        Task<bool> RequestBluetoothPermissions();
        Task<bool> RequestLocationPermissions();
        bool IsBluetoothSupported();
        bool IsServerSupported();
        bool IsBluetoothApiLevelSufficient();
        Task<bool> TurnOnBluetooth();
        Task<bool> TurnOnLocationServices(); 
        Task AttemptConnection(PartnerDevice partnerDevice);
        void ForgetDevice(PartnerDevice partnerDevice);
        IEnumerable<PartnerDevice> GetPairedDevices();
        Task<bool> StartScanning();
        event EventHandler<PartnerDevice> DeviceDiscovered;
        Task<bool> BecomeDiscoverable();
        Task ListenForConnections();
        Task<bool> ListenForFov();
        event EventHandler Connected;
        event EventHandler Disconnected;

        Task SendPayload(byte[] bytes);
        event EventHandler<byte[]> PayloadReceived;
    }

    public class PartnerDevice
    {
        public string Name { get; set; }
        public string Address { get; set; }
    }
}