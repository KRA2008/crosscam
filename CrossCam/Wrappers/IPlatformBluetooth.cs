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
        Task SendReadyForPreviewFrame();
        Task SendPreviewFrame(byte[] frame);
        Task SendReadyForClockReading();
        Task SendClockReading();
        void ProcessClockReading(byte[] readingBytes);
        Task SendSync(DateTime syncMoment);
        Task ProcessSyncAndCapture(byte[] syncBytes);
        void ForgetDevice(PartnerDevice partnerDevice);
        Task SendCaptue(byte[] capturedImage);
        IEnumerable<PartnerDevice> GetPairedDevices();
        Task<bool> StartScanning();
        event EventHandler<PartnerDevice> DeviceDiscovered;
        Task<bool> BecomeDiscoverable();
        Task ListenForConnections();
        Task<bool> SendFov(double fov);
        Task<bool> ListenForFov();
        void SendSecondaryErrorOccurred();
        event EventHandler Connected;
        event EventHandler Disconnected;
        event EventHandler<double> FovReceived;
        event EventHandler PreviewFrameRequested;
        event EventHandler<byte[]> PreviewFrameReceived;
        event EventHandler<long> ClockReadingReceived;
        event EventHandler<DateTime> SyncReceived;
        event EventHandler<byte[]> CaptureReceived; 
        event EventHandler SecondaryErrorReceived;
    }

    public class PartnerDevice
    {
        public string Name { get; set; }
        public string Address { get; set; }
    }
}