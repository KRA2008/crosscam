﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
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

        public Task SendPayload(byte[] bytes)
        {
            NSError error = null;
            _session?.SendData(NSData.FromArray(bytes), _session.ConnectedPeers, MCSessionSendDataMode.Reliable, out error);
            if (error != null)
            {
                throw new Exception(error.ToString());
            }
            return Task.FromResult(true);
        }

        public event EventHandler<byte[]> PayloadReceived;
        private void OnReceivedPayload(byte[] bytes)
        {
            var handler = PayloadReceived;
            handler?.Invoke(this, bytes);
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

        public IEnumerable<PartnerDevice> GetPairedDevices()
        {
            return Enumerable.Empty<PartnerDevice>().ToList();
        }

        public Task<bool> StartScanning()
        {
            var myPeerId = new MCPeerID(UIDevice.CurrentDevice.Name);
            _session = new MCSession(myPeerId) { Delegate = new SessionDelegate(this) };
            var browser = new MCNearbyServiceBrowser(myPeerId, BluetoothOperator.CROSSCAM_SERVICE)
            {
                Delegate = new NewBrowserDelegate(this)
            };
            browser.StartBrowsingForPeers();
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

        public Task<bool> ListenForFov()
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
                _platformBluetooth.OnReceivedPayload(data.ToArray());
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