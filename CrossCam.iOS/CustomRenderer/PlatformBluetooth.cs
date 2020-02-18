using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
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
        private const string CROSSCAM_SERVICE = "CrossCam";

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

        public event EventHandler<ConnectedEventArgs> Connected;
        private void OnConnected(bool? isPrimary)
        {
            var handler = Connected;
            handler?.Invoke(this, new ConnectedEventArgs
            {
                IsPrimary = isPrimary
            });
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
            return Task.FromResult(true);
        }

        public Task ListenForConnections()
        {
            return Task.FromResult(true);
        }

        public async Task<bool> SayHello()
        {
            await Task.Delay(3000);
            var data = NSData.FromString("Hi there friend.");
            _session.SendData(data, _session.ConnectedPeers, MCSessionSendDataMode.Reliable, out var error);
            if (error != null)
            {
                throw new Exception(error.ToString());
            }
            return true;
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
                        _platformBluetooth.OnConnected(null);
                        _platformBluetooth.SayHello();
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
                InvokeOnMainThread(() => {
                    var alert = new UIAlertView("", data.ToString(), null, "OK");
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