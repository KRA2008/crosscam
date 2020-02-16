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
        Task<bool> AttemptConnection(PartnerDevice partnerDevice);
        void ForgetDevice(PartnerDevice partnerDevice);
        IEnumerable<PartnerDevice> GetPairedDevices();
        bool StartScanning();
        event EventHandler<PartnerDevice> DeviceDiscovered;
        Task<bool> BecomeDiscoverable();
        Task<bool?> ListenForConnections();
        Task<bool> SayHello();
        Task<bool> ListenForHello();
    }

    public class PartnerDevice
    {
        public string Name { get; set; }
        public string Address { get; set; }
    }
}