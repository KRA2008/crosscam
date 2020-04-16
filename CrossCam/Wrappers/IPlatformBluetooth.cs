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
        bool StartScanning();
        event EventHandler<PartnerDevice> DeviceDiscovered;
        Task<bool> BecomeDiscoverable();
        Task ListenForConnections();
        Task<bool> SayHello();
        Task<bool> ListenForHello();
        event EventHandler Connected;
        event EventHandler Disconnected;
        event EventHandler HelloReceived;
        event EventHandler PreviewFrameRequested;
        event EventHandler<byte[]> PreviewFrameReceived;
        event EventHandler<long> ClockReadingReceived;
        event EventHandler<DateTime> SyncReceived;
        event EventHandler<byte[]> CaptureReceived;
    }

    public class PartnerDevice
    {
        public string Name { get; set; }
        public string Address { get; set; }
    }
}