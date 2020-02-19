using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        public bool IsPrimary { get; set; }

        private const string CROSSCAM_SERVICE = "CrossCam";
        private const int HEADER_LENGTH = 6;
        private const byte SYNC_MASK = 170; // 0xAA (do it twice)
        protected enum CrossCommand
        {
            Hello = 1,
            ReadyForPreviewFrame,
            PreviewFrame,
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


        public event EventHandler<PartnerDevice> DeviceDiscovered;
        private void OnDeviceDiscovered(PartnerDevice e)
        {
            var handler = DeviceDiscovered;
            handler?.Invoke(this, e);
        }

        public void Disconnect()
        {
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

        public IEnumerable<PartnerDevice> GetPairedDevices()
        {
            return Enumerable.Empty<PartnerDevice>().ToList();
        }

        public bool StartScanning()
        {
            var myPeerId = new MCPeerID(UIDevice.CurrentDevice.Name);
            var session = new MCSession(myPeerId) {Delegate = new SessionDelegate(this)};
            var browser = new MCBrowserViewController(CROSSCAM_SERVICE, session)
            {
                Delegate = new BrowserViewControllerDelegate(),
                ModalPresentationStyle = UIModalPresentationStyle.FormSheet
            };
            var rootVc = UIApplication.SharedApplication.KeyWindow.RootViewController;
            rootVc.PresentViewController(browser, true, null);
            IsPrimary = true;
            return true;
        }

        public void ForgetDevice(PartnerDevice partnerDevice)
        {
        }

        public Task<bool> BecomeDiscoverable()
        {
            var myPeerId = new MCPeerID(UIDevice.CurrentDevice.Name);
            var session = new MCSession(myPeerId) {Delegate = new SessionDelegate(this)};
            var assistant = new MCAdvertiserAssistant(CROSSCAM_SERVICE, new NSDictionary(),  session);
            assistant.Start();
            IsPrimary = false;
            return Task.FromResult(true);
        }

        public Task ListenForConnections()
        {
            return Task.FromResult(true);
        }

        public async Task<bool> SayHello()
        {
            var payloadBytes = Encoding.UTF8.GetBytes("Hi there friend.");
            var messageFrame = GetMessageFrame(CrossCommand.Hello, payloadBytes);

            _session.SendData(NSData.FromArray(messageFrame), _session.ConnectedPeers, MCSessionSendDataMode.Reliable, out var error);
            if (error != null)
            {
                throw new Exception(error.ToString());
            }
            return true;
        }

        private byte[] GetMessageFrame(CrossCommand crossCommand, byte[] payload)
        {
            var payloadLength = payload.Length;
            var frame = new List<byte>
            {
                SYNC_MASK,
                SYNC_MASK,
                (byte) crossCommand,
                (byte) (payloadLength >> 8),
                (byte) (payloadLength >> 4),
                (byte) payloadLength
            };
            return frame.Concat(payload).ToArray();
        }

        public Task<bool> ListenForHello()
        {
            return Task.FromResult(true);
        }

        public Task AttemptConnection(PartnerDevice partnerDevice)
        {
            throw new NotImplementedException();
        }

        public bool IsServerSupported()
        {
            return true;
        }

        public bool IsBluetoothApiLevelSufficient()
        {
            return true;
        }

        public Task<bool> SendPreviewFrame(byte[] preview)
        {
            throw new NotImplementedException();
        }

        public Task<byte[]> Capture(int countdownSeconds)
        {
            throw new NotImplementedException();
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
                        _platformBluetooth._session = session;
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
                    switch (bytes[2])
                    {
                        case (int)CrossCommand.Hello:
                            HandleHelloCommand(bytes, payloadLength);
                            break;
                        case (int)CrossCommand.PreviewFrame:
                            break;
                        case (int)CrossCommand.ReadyForPreviewFrame:
                            break;
                    }
                }
            }

            private void HandleHelloCommand(byte[] bytes, int payloadLength)
            {
                var helloMessage = Encoding.UTF8.GetString(bytes, HEADER_LENGTH, payloadLength);
                InvokeOnMainThread(() => {
                    var alert = new UIAlertView("", helloMessage, null, "OK");
                    alert.Show();
                });
            }
        }

        private class BrowserViewControllerDelegate : MCBrowserViewControllerDelegate
        {
            public override void DidFinish(MCBrowserViewController browserViewController)
            {
                InvokeOnMainThread(() => {
                    browserViewController.DismissViewController(true, null);
                });
            }

            public override void WasCancelled(MCBrowserViewController browserViewController)
            {
                InvokeOnMainThread(() => {
                    browserViewController.DismissViewController(true, null);
                });
            }
        }
    }
}