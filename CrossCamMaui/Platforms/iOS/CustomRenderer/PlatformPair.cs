using System.Diagnostics;
using System.Runtime.InteropServices;
using CrossCam.CustomElement;
using CrossCam.Wrappers;
using Foundation;
using Microsoft.AppCenter.Analytics;
using MultipeerConnectivity;
using UIKit;
using ErrorEventArgs = CrossCam.CustomElement.ErrorEventArgs;

namespace CrossCam.Platforms.iOS.CustomRenderer
{
    public class PlatformPair : IPlatformPair
    {
        private MCSession _session;
        private MCNearbyServiceBrowser _serviceBrowser; // technically these two could be variables in methods, but then iOS sometimes clips them off and it doesn't connect
        private MCAdvertiserAssistant _advertiserAssistant;

        public event EventHandler Connected;
        private void OnConnected()
        {
            Connected?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler Disconnected;
        private void OnDisconnected()
        {
            Disconnected?.Invoke(this, EventArgs.Empty);
        }

        public async void SendPayload(byte[] bytes)
        {
            try
            {
                NSError error = null;
                if ((PairOperator.CrossCommand) bytes[2] == PairOperator.CrossCommand.CapturedImage)
                {
                    await Task.Delay(1000);
                }

                _session?.SendData(NSData.FromArray(bytes), _session.ConnectedPeers, MCSessionSendDataMode.Reliable,
                    out error); //TODO: how to indicate transmitting on secondary?
                if (error != null)
                {
                    if (error.Code == 2)
                    {
                        Analytics.TrackEvent("Paired endpoint error code 2 (probably just a disconnect)");
                    }
                    else
                    {
                        throw new Exception(error.ToString());
                    }
                }
            }
            catch (Exception e)
            {
                OnErrorOccurred(new ErrorEventArgs
                {
                    Exception = e,
                    Step = "Send payload"
                });
            }
        }

        public event EventHandler<ErrorEventArgs> ErrorOccurred;
        private void OnErrorOccurred(ErrorEventArgs error)
        {
            ErrorOccurred?.Invoke(this, error);
        }

        public event EventHandler<byte[]> PayloadReceived;
        private void OnPayloadReceived(byte[] bytes)
        {
            PayloadReceived?.Invoke(this, bytes);
        }

        public void Disconnect()
        {
            _session?.Disconnect();
            OnDisconnected();
        }

        public Task StartScanning()
        {
            var isWifiEnabled = NetworkInterfaces.IsWifiEnabled();
            if(!isWifiEnabled) throw new WiFiTurnedOffException();

            var myPeerId = new MCPeerID(UIDevice.CurrentDevice.Name);
            _session = new MCSession(myPeerId) { Delegate = new SessionDelegate(this) };
            _serviceBrowser = new MCNearbyServiceBrowser(myPeerId, PairOperator.CROSSCAM_SERVICE)
            {
                Delegate = new BrowserDelegate(this)
            };
            _serviceBrowser.StartBrowsingForPeers();
            //var browser = new MCBrowserViewController(PairOperator.CROSSCAM_SERVICE, _session)
            //{
            //    Delegate = new BrowserViewControllerDelegate(),
            //    MaximumNumberOfPeers = 1,
            //    MinimumNumberOfPeers = 1
            //};
            //AppDelegate.Instance.Window.RootViewController?.PresentViewController(browser, true, null);
            Debug.WriteLine("### SCANNING START");
            return Task.FromResult(true);
        }

        public Task BecomeDiscoverable()
        {
            var isWifiEnabled = NetworkInterfaces.IsWifiEnabled();
            if (!isWifiEnabled) throw new WiFiTurnedOffException();

            var myPeerId = new MCPeerID(UIDevice.CurrentDevice.Name);
            _session = new MCSession(myPeerId) {Delegate = new SessionDelegate(this)};
            var discoveryInfo = new NSMutableDictionary<NSString,NSString>();
            discoveryInfo.Add(new NSString("Device Type"),new NSString(UIDevice.CurrentDevice.Name));
            discoveryInfo.Add(new NSString("OS"), new NSString(UIDevice.CurrentDevice.SystemName));
            discoveryInfo.Add(new NSString("OS Version"), new NSString(UIDevice.CurrentDevice.SystemVersion));
            _advertiserAssistant = new MCAdvertiserAssistant(PairOperator.CROSSCAM_SERVICE, discoveryInfo, _session);
            _advertiserAssistant.Delegate = new AdvertiserAssistantDelegate();
            _advertiserAssistant.Start();
            //var advertiser = new MCNearbyServiceAdvertiser(myPeerId, discoveryInfo, PairOperator.CROSSCAM_SERVICE);
            //advertiser.Delegate = new NearbyServiceAdvertiserDelegate();
            //advertiser.StartAdvertisingPeer();
            Debug.WriteLine("### DISCOVERABLE START"); 
            return Task.FromResult(true);
        }

        private class SessionDelegate : MCSessionDelegate
        {
            private readonly PlatformPair _platformPair;

            public SessionDelegate(PlatformPair platformPair)
            {
                _platformPair = platformPair;
            }

            public override void DidChangeState(MCSession session, MCPeerID peerID, MCSessionState state)
            {
                switch (state)
                {
                    case MCSessionState.Connected:
                        Debug.WriteLine("### Connected to " + peerID.DisplayName);
                        _platformPair.OnConnected();
                        break;
                    case MCSessionState.Connecting:
                        Debug.WriteLine("### Connecting to " + peerID.DisplayName);
                        break;
                    case MCSessionState.NotConnected:
                        Debug.WriteLine("### Not connected to " + peerID.DisplayName);
                        if (_platformPair._session != null)
                        {
                            _platformPair.OnDisconnected();
                        }
                        _platformPair._session = null;
                        break;
                    default:
                        Debug.WriteLine("### Unknown state change! " + state);
                        break;
                }
                Debug.WriteLine("### stop browsing and stop being discoverable");
                _platformPair._advertiserAssistant?.Stop();
                _platformPair._serviceBrowser?.StopBrowsingForPeers();
            }

            public override void DidStartReceivingResource(MCSession session, string resourceName, MCPeerID fromPeer, NSProgress progress)
            {
                //Debug.WriteLine("### DATA START RECEIVING");
            }

            public override void DidFinishReceivingResource(MCSession session, string resourceName, MCPeerID fromPeer, NSUrl localUrl,
                NSError error)
            {
                //Debug.WriteLine("### DATA RECEIVE FINISH");
                //if (error != null)
                //{
                //    Debug.WriteLine("### DATA RECEIVE HAD ERROR: " + error);
                //}
            }

            public override void DidReceiveStream(MCSession session, NSInputStream stream, string streamName, MCPeerID peerID)
            {
                //Debug.WriteLine("### STREAM RECEIVE");
            }

            public override void DidReceiveData(MCSession session, NSData data, MCPeerID peerID)
            {
                //Debug.WriteLine("### DATA RECEIVED");
                _platformPair.OnPayloadReceived(data.ToArray());
            }
        }

        private class BrowserDelegate : MCNearbyServiceBrowserDelegate
        {
            private readonly PlatformPair _platformPair;

            public BrowserDelegate(PlatformPair platformPair)
            {
                _platformPair = platformPair;
            }

            public override void DidNotStartBrowsingForPeers(MCNearbyServiceBrowser browser, NSError error)
            {
                Debug.WriteLine("### DID NOT START BROWSING: " + error);
            }

            public override void FoundPeer(MCNearbyServiceBrowser browser, MCPeerID peerID, NSDictionary info)
            {
                if (_platformPair._session != null)
                {
                    Debug.WriteLine("### FOUND PEER: " + peerID.DisplayName);
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        browser.InvitePeer(peerID, _platformPair._session, null, 30);
                    });
                    Debug.WriteLine("### stop browsing");
                    browser.StopBrowsingForPeers();
                }
            }

            public override void LostPeer(MCNearbyServiceBrowser browser, MCPeerID peerID)
            {
                Debug.WriteLine("### LOST PEER: " + peerID.DisplayName);
            }
        }

        private class BrowserViewControllerDelegate : MCBrowserViewControllerDelegate
        {
            public override void WasCancelled(MCBrowserViewController browserViewController)
            {
                Debug.WriteLine("### MCBrowserVC was cancelled...");
                browserViewController.DismissViewController(true, null);
            }

            public override void DidFinish(MCBrowserViewController browserViewController)
            {
                Debug.WriteLine("### MCBrowserVC finished...");
                browserViewController.DismissViewController(true, null);
            }
        }

        private class NearbyServiceAdvertiserDelegate : MCNearbyServiceAdvertiserDelegate
        {
            public override void DidNotStartAdvertisingPeer(MCNearbyServiceAdvertiser advertiser, NSError error)
            {
                Debug.WriteLine("### ADVERTISING FAILED: " + error);
            }

            public override void WillChangeValue(string forKey)
            {
                Debug.WriteLine("### ADVERTISER WILL CHANGE KEY: " + forKey);
            }

            public override void DidReceiveInvitationFromPeer(MCNearbyServiceAdvertiser advertiser, MCPeerID peerID, NSData? context,
                MCNearbyServiceAdvertiserInvitationHandler invitationHandler)
            {
                Debug.WriteLine("### INVITATION RECEIVED FROM: " + peerID.DisplayName);
            }
        }

        private class AdvertiserAssistantDelegate : MCAdvertiserAssistantDelegate
        {
            public override void WillPresentInvitation(MCAdvertiserAssistant advertiserAssistant)
            {
                Debug.WriteLine("### WILL PRESENT INVITATION");
            }

            public override void DidDismissInvitation(MCAdvertiserAssistant advertiserAssistant)
            {
                Debug.WriteLine("### DID DISMISS INVITATION");
            }

            public override void WillChangeValue(string forKey)
            {
                Debug.WriteLine("### ADVERTISER WILL CHANGE VALUE: " + forKey);
            }
        }

        private class NetworkInterfaces
        {
            //from https://gist.github.com/brendanzagaeski/9979929

            struct ifaddrs
            {
                public IntPtr ifa_next;
                public string ifa_name;
                public uint ifa_flags;
                public IntPtr ifa_addr;
                public IntPtr ifa_netmask;
                public IntPtr ifa_dstaddr;
                public IntPtr ifa_data;
            }

            [DllImport("libc")]
            static extern int getifaddrs(out IntPtr ifap);

            [DllImport("libc")]
            static extern void freeifaddrs(IntPtr ifap);

            public static bool IsWifiEnabled()
            {
                var awdl0Count = 0;

                if (getifaddrs(out var ifap) != 0) return true; // we can't tell, just go on.

                    try
                {
                    var next = ifap;
                    while (next != IntPtr.Zero)
                    {
                        var addr = (ifaddrs)Marshal.PtrToStructure(next, typeof(ifaddrs));

                        if (addr.ifa_name == "awdl0")
                        {
                            awdl0Count++;
                        }

                        next = addr.ifa_next;
                    }
                }
                finally
                {
                    freeifaddrs(ifap);
                }

                return awdl0Count == 2;
            }
        }
    }
}