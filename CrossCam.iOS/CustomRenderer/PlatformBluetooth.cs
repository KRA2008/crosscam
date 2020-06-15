using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrossCam.CustomElement;
using CrossCam.iOS.CustomRenderer;
using CrossCam.Wrappers;
using ExternalAccessory;
using Foundation;
using MultipeerConnectivity;
using UIKit;
using Xamarin.Forms;

[assembly: Dependency(typeof(PlatformBluetooth))]
namespace CrossCam.iOS.CustomRenderer
{
    public class PlatformBluetooth : IPlatformBluetooth
    {
        private const string HELLO_MESSAGE = "Hi there friend.";
        private const int HEADER_LENGTH = 6;
        private const byte SYNC_MASK = 170; // 0xAA (do it twice)
        protected enum CrossCommand
        {
            Fov = 1,
            ReadyForPreviewFrame,
            PreviewFrame,
            ReadyForClockReading,
            ClockReading,
            Sync,
            CapturedImage,
            Error
        }

        private readonly ObservableCollection<EAAccessory> _availableDevices =
            new ObservableCollection<EAAccessory>();
        private MCSession _session;

        public PlatformBluetooth()
        {
            _availableDevices.CollectionChanged += (sender, args) =>
            {
                if (args.Action == NotifyCollectionChangedAction.Add)
                {
                    foreach (var newItem in args.NewItems)
                    {
                        var newDevice = (EAAccessory)newItem;
                        OnDeviceDiscovered(new PartnerDevice
                        {
                            Name = newDevice.Name ?? "Unnamed",
                            Address = newDevice.Description //TODO???? and switch to using this for IDing connection attempt below
                        });
                    }
                }
            };
        }

        public event EventHandler Connected;
        private void OnConnected()
        {
            var handler = Connected;
            handler?.Invoke(this, new EventArgs());
        }

        public event EventHandler Disconnected;
        private void OnDisconnected()
        {
            var handler = Disconnected;
            handler?.Invoke(this, new EventArgs());
        }

        public event EventHandler<double> FovReceived;
        private void OnFovReceived(double fov)
        {
            var handler = FovReceived;
            handler?.Invoke(this, fov);
        }

        public event EventHandler PreviewFrameRequested;
        private void OnPreviewFrameRequested()
        {
            var handler = PreviewFrameRequested;
            handler?.Invoke(this, new EventArgs());
        }

        public event EventHandler<byte[]> PreviewFrameReceived;
        private void OnPreviewFrameReceived(byte[] bytes)
        {
            var handler = PreviewFrameReceived;
            handler?.Invoke(this, bytes);
        }

        public event EventHandler<long> ClockReadingReceived;
        private void OnClockReadingReceived(long e)
        {
            var handler = ClockReadingReceived;
            handler?.Invoke(this, e);
        }

        public event EventHandler<PartnerDevice> DeviceDiscovered;
        private void OnDeviceDiscovered(PartnerDevice e)
        {
            var handler = DeviceDiscovered;
            handler?.Invoke(this, e);
        }

        public event EventHandler<DateTime> SyncReceived;
        private void OnSyncReceived(DateTime e)
        {
            var handler = SyncReceived;
            handler?.Invoke(this, e);
        }

        public event EventHandler<byte[]> CaptureReceived;
        private void OnCaptureReceived(byte[] e)
        {
            var handler = CaptureReceived;
            handler?.Invoke(this, e);
        }

        public event EventHandler SecondaryErrorReceived;
        private void OnSecondaryErrorReceived()
        {
            var handler = SecondaryErrorReceived;
            handler?.Invoke(this, new EventArgs());
        }


        public void Disconnect()
        {
            _session.Disconnect();
        }

        public Task<bool> RequestBluetoothPermissions()
        {
            return Task.FromResult(true);
        }

        public Task<bool> RequestLocationPermissions()
        {
            return Task.FromResult(true);
        }

        public bool IsBluetoothSupported()
        {
            return true;
        }
        public Task<bool> TurnOnBluetooth()
        {
            return Task.FromResult(true);
        }

        public Task<bool> TurnOnLocationServices()
        {
            return Task.FromResult(true);
        }

        public Task SendCaptue(byte[] capturedImage)
        {
            var message = AddPayloadHeader(CrossCommand.CapturedImage, capturedImage);
            SendData(message);
            return Task.FromResult(true);
        }

        public IEnumerable<PartnerDevice> GetPairedDevices()
        {
            return Enumerable.Empty<PartnerDevice>().ToList();
        }

        public bool StartScanning()
        {
            var myPeerId = new MCPeerID(UIDevice.CurrentDevice.Name);
            _session = new MCSession(myPeerId) { Delegate = new SessionDelegate(this) };
            var browser = new MCNearbyServiceBrowser(myPeerId, BluetoothOperator.CROSSCAM_SERVICE)
            {
                Delegate = new NewBrowserDelegate(this)
            };
            browser.StartBrowsingForPeers();
            return true;
        }

        public Task SendClockReading()
        {
            var ticks = DateTime.UtcNow.Ticks;
            var message = AddPayloadHeader(CrossCommand.ClockReading, BitConverter.GetBytes(ticks));
            SendData(message);
            return Task.FromResult(true);
        }

        public void ProcessClockReading(byte[] readingBytes)
        {
            var reading = BitConverter.ToInt64(readingBytes);
            OnClockReadingReceived(reading);
        }

        public Task SendSync(DateTime syncMoment)
        {
            var syncMessage = AddPayloadHeader(CrossCommand.Sync, BitConverter.GetBytes(syncMoment.Ticks));
            SendData(syncMessage);
            return Task.FromResult(true);
        }

        public Task ProcessSyncAndCapture(byte[] syncBytes)
        {
            OnSyncReceived(new DateTime(BitConverter.ToInt64(syncBytes)));
            return Task.FromResult(true);
        }

        public void ForgetDevice(PartnerDevice partnerDevice)
        {
        }

        public Task<bool> BecomeDiscoverable()
        {
            var myPeerId = new MCPeerID(UIDevice.CurrentDevice.Name);
            _session = new MCSession(myPeerId) {Delegate = new SessionDelegate(this)};
            var assistant = new MCAdvertiserAssistant(BluetoothOperator.CROSSCAM_SERVICE, new NSDictionary(), _session);
            assistant.Start();
            return Task.FromResult(true);
        }

        public Task ListenForConnections()
        {
            return Task.FromResult(true);
        }

        public Task<bool> SendFov(double fov)
        {
            var payloadBytes = BitConverter.GetBytes(fov);
            var fullMessage = AddPayloadHeader(CrossCommand.Fov, payloadBytes);

            SendData(fullMessage);
            return Task.FromResult(true);
        }

        private static byte[] AddPayloadHeader(CrossCommand crossCommand, byte[] payload)
        {
            var payloadLength = payload.Length;
            var header = new List<byte>
            {
                SYNC_MASK,
                SYNC_MASK,
                (byte) crossCommand,
                (byte) (payloadLength >> 8),
                (byte) (payloadLength >> 4),
                (byte) payloadLength
            };
            return header.Concat(payload).ToArray();
        }

        private void SendData(byte[] payload)
        {
            NSError error = null;
            _session?.SendData(NSData.FromArray(payload), _session.ConnectedPeers, MCSessionSendDataMode.Reliable, out error);
            if (error != null)
            {
                throw new Exception(error.ToString());
            }
        }

        public Task<bool> ListenForFov()
        {
            return Task.FromResult(true);
        }

        public Task AttemptConnection(PartnerDevice partnerDevice)
        {
            throw new NotImplementedException();
        }

        public Task SendReadyForPreviewFrame()
        {
            var fullMessage = AddPayloadHeader(CrossCommand.ReadyForPreviewFrame, Enumerable.Empty<byte>().ToArray());
            SendData(fullMessage);
            return Task.FromResult(true);
        }

        public Task SendPreviewFrame(byte[] frame)
        {
            var frameMessage = AddPayloadHeader(CrossCommand.PreviewFrame, frame);
            SendData(frameMessage);
            return Task.FromResult(true);
        }

        public Task SendReadyForClockReading()
        {
            var message = AddPayloadHeader(CrossCommand.ReadyForClockReading, Enumerable.Empty<byte>().ToArray());
            SendData(message);
            return Task.FromResult(true);
        }

        public void SendSecondaryErrorOccurred()
        {
            SendData(AddPayloadHeader(CrossCommand.Error, Enumerable.Empty<byte>().ToArray()));
        }

        public bool IsServerSupported()
        {
            return true;
        }

        public bool IsBluetoothApiLevelSufficient()
        {
            return true;
        }

        private class SessionDelegate : MCSessionDelegate
        {
            private readonly PlatformBluetooth _platformBluetooth;

            public SessionDelegate(PlatformBluetooth platformBluetooth)
            {
                _platformBluetooth = platformBluetooth;
            }

            public override void DidChangeState(MCSession session, MCPeerID peerID, MCSessionState state)
            {
                switch (state)
                {
                    case MCSessionState.Connected:
                        Debug.WriteLine("Connected to " + peerID.DisplayName);
                        _platformBluetooth.OnConnected();
                        break;
                    case MCSessionState.Connecting:
                        Debug.WriteLine("Connecting to " + peerID.DisplayName);
                        break;
                    case MCSessionState.NotConnected:
                        if (_platformBluetooth._session != null)
                        {
                            _platformBluetooth.OnDisconnected();
                        }
                        _platformBluetooth._session = null;
                        Debug.WriteLine("Not Connected to " + peerID.DisplayName);
                        break;
                }
            }

            public override void DidStartReceivingResource(MCSession session, string resourceName, MCPeerID fromPeer, NSProgress progress)
            {
            }

            public override void DidFinishReceivingResource(MCSession session, string resourceName, MCPeerID fromPeer, NSUrl localUrl,
                NSError error)
            {
            }

            public override void DidReceiveStream(MCSession session, NSInputStream stream, string streamName, MCPeerID peerID)
            {
            }

            public override void DidReceiveData(MCSession session, NSData data, MCPeerID peerID)
            {
                var bytes = data.ToArray();
                if (bytes[0] == SYNC_MASK &&
                    bytes[1] == SYNC_MASK)
                {
                    var payloadLength = (bytes[3] << 8) | (bytes[4] << 4) | bytes[5];
                    var payload = bytes.Skip(HEADER_LENGTH).ToArray();
                    if (payload.Length != payloadLength)
                    {
                        //panic
                    }
                    switch (bytes[2])
                    {
                        case (int)CrossCommand.Fov:
                            HandleFovReceived(payload);
                            break;
                        case (int)CrossCommand.PreviewFrame:
                            _platformBluetooth.OnPreviewFrameReceived(payload);
                            break;
                        case (int)CrossCommand.ReadyForPreviewFrame:
                            _platformBluetooth.OnPreviewFrameRequested();
                            break;
                        case (int)CrossCommand.ReadyForClockReading:
                            _platformBluetooth.SendClockReading();
                            break;
                        case (int)CrossCommand.ClockReading:
                            _platformBluetooth.ProcessClockReading(payload);
                            break;
                        case (int)CrossCommand.Sync:
                            _platformBluetooth.ProcessSyncAndCapture(payload);
                            break;
                        case (int)CrossCommand.CapturedImage:
                            _platformBluetooth.OnCaptureReceived(payload);
                            break;
                        case (int)CrossCommand.Error:
                            _platformBluetooth.OnSecondaryErrorReceived();
                            break;
                    }
                }
            }

            private void HandleFovReceived(byte[] bytes)
            {
                Debug.WriteLine("Received fov");
                var fov = BitConverter.ToDouble(bytes, 0);
                _platformBluetooth.OnConnected();
                _platformBluetooth.OnFovReceived(fov);
            }
        }

        private class NewBrowserDelegate : MCNearbyServiceBrowserDelegate
        {
            private readonly PlatformBluetooth _platformBluetooth;

            public NewBrowserDelegate(PlatformBluetooth platformBluetooth)
            {
                _platformBluetooth = platformBluetooth;
            }

            public override void FoundPeer(MCNearbyServiceBrowser browser, MCPeerID peerID, NSDictionary info)
            {
                browser.InvitePeer(peerID, _platformBluetooth._session, null, 30);
                browser.StopBrowsingForPeers();
            }

            public override void LostPeer(MCNearbyServiceBrowser browser, MCPeerID peerID)
            {
            }
        }
    }
}