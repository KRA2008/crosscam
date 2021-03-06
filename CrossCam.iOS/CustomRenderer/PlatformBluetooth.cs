﻿using System;
using System.Diagnostics;
using System.Threading.Tasks;
using CrossCam.CustomElement;
using CrossCam.iOS.CustomRenderer;
using CrossCam.Wrappers;
using Foundation;
using MultipeerConnectivity;
using UIKit;
using Xamarin.Forms;
using Xamarin.Essentials;

[assembly: Dependency(typeof(PlatformBluetooth))]
namespace CrossCam.iOS.CustomRenderer
{
    public class PlatformBluetooth : IPlatformBluetooth
    {
        private MCSession _session;

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

        public async void SendPayload(byte[] bytes)
        {
            NSError error = null;
            if ((BluetoothOperator.CrossCommand)bytes[2] == BluetoothOperator.CrossCommand.CapturedImage)
            {
                await Task.Delay(1000);
            }
            _session?.SendData(NSData.FromArray(bytes), _session.ConnectedPeers, MCSessionSendDataMode.Reliable, out error); //TODO: how to indicate transmitting on secondary?
            if (error != null)
            {
                throw new Exception(error.ToString());
            }
        }

        public event EventHandler<byte[]> PayloadReceived;
        private void OnPayloadReceived(byte[] bytes)
        {
            var handler = PayloadReceived;
            handler?.Invoke(this, bytes);
        }

        public void Disconnect()
        {
            _session.Disconnect();
            OnDisconnected();
        }

        public Task<string> StartScanning()
        {
            var myPeerId = new MCPeerID(UIDevice.CurrentDevice.Name);
            _session = new MCSession(myPeerId) { Delegate = new SessionDelegate(this) };
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var browser = new MCNearbyServiceBrowser(myPeerId, BluetoothOperator.CROSSCAM_SERVICE)
                {
                    Delegate = new NewBrowserDelegate(this)
                };
                browser.StartBrowsingForPeers();
            });
            Debug.WriteLine("### SCANNING START");
            return Task.FromResult((string)null);
        }

        public Task<bool> BecomeDiscoverable()
        {
            var myPeerId = new MCPeerID(UIDevice.CurrentDevice.Name);
            _session = new MCSession(myPeerId) {Delegate = new SessionDelegate(this)};
            var assistant = new MCAdvertiserAssistant(BluetoothOperator.CROSSCAM_SERVICE, new NSDictionary(), _session);
            assistant.Start();
            Debug.WriteLine("### DISCOVERABLE START");
            return Task.FromResult(true);
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
                //Debug.WriteLine("### DATA START RECEIVING");
            }

            public override void DidFinishReceivingResource(MCSession session, string resourceName, MCPeerID fromPeer, NSUrl localUrl,
                NSError error)
            {
                //Debug.WriteLine("### DATA RECEIVE FINISH");
            }

            public override void DidReceiveStream(MCSession session, NSInputStream stream, string streamName, MCPeerID peerID)
            {
                //Debug.WriteLine("### STREAM RECEIVE");
            }

            public override void DidReceiveData(MCSession session, NSData data, MCPeerID peerID)
            {
                //Debug.WriteLine("### DATA RECEIVED");
                _platformBluetooth.OnPayloadReceived(data.ToArray());
            }
        }

        private class NewBrowserDelegate : MCNearbyServiceBrowserDelegate
        {
            private readonly PlatformBluetooth _platformBluetooth;

            public NewBrowserDelegate(PlatformBluetooth platformBluetooth)
            {
                _platformBluetooth = platformBluetooth;
            }

            public override void DidNotStartBrowsingForPeers(MCNearbyServiceBrowser browser, NSError error)
            {
                Debug.WriteLine("### DID NOT START BROWSING: " + error);
            }

            public override void FoundPeer(MCNearbyServiceBrowser browser, MCPeerID peerID, NSDictionary info)
            {
                if (_platformBluetooth._session != null)
                {
                    Debug.WriteLine("### FOUND PEER: " + peerID.DisplayName);
                    browser.InvitePeer(peerID, _platformBluetooth._session, null, 30);
                    browser.StopBrowsingForPeers();
                }
            }

            public override void LostPeer(MCNearbyServiceBrowser browser, MCPeerID peerID)
            {
                Debug.WriteLine("### LOST PEER: " + peerID.DisplayName);
            }
        }
    }
}