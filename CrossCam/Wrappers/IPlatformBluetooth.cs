using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CrossCam.Wrappers
{
    public interface IPlatformBluetooth
    {
        bool IsPrimary { get; set; }
        void Disconnect();
        Task<bool> RequestBluetoothPermissions();
        Task<bool> RequestLocationPermissions();
        bool IsBluetoothSupported();
        bool IsServerSupported();
        bool IsBluetoothApiLevelSufficient();
        Task<bool> TurnOnBluetooth();
        Task<bool> TurnOnLocationServices(); 
        Task AttemptConnection(PartnerDevice partnerDevice);
        Task SendReadyForPreviewFrame();
        Task SendPreviewFrame(byte[] frame);
        void ForgetDevice(PartnerDevice partnerDevice);
        IEnumerable<PartnerDevice> GetPairedDevices();
        bool StartScanning();
        event EventHandler<PartnerDevice> DeviceDiscovered;
        Task<bool> BecomeDiscoverable();
        Task ListenForConnections();
        Task<bool> SayHello();
        Task<bool> ListenForHello();
        event EventHandler Connected;
        event EventHandler Disconnected;
        event EventHandler PreviewFrameRequested;
        event EventHandler<byte[]> PreviewFrameReceived;
    }

    public class PartnerDevice
    {
        public string Name { get; set; }
        public string Address { get; set; }
    }
}