using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CrossCam.Wrappers
{
    public interface IPlatformBluetooth
    {
        bool IsConnected();
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
        List<PartnerDevice> GetPairedDevices();
        bool BeginSearchForDiscoverableDevices();
        event EventHandler<PartnerDevice> DeviceDiscovered;
        Task<bool> BecomeDiscoverable();
        Task<bool?> ListenForConnections();
    }

    public class PartnerDevice
    {
        public const string SDP_UUID = "492a8e3d-2589-40b1-b9c2-419a7ce80f3c";
        public string Name { get; set; }
        public string Address { get; set; }
    }
}