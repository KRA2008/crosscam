using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Android.Bluetooth;
using Android.Content;
using Android.Gms.Common;
using Android.Gms.Common.Apis;
using Android.Gms.Location;
using Android.Gms.Nearby;
using Android.Gms.Nearby.Connection;
using Android.OS;
using AndroidX.AppCompat.App;
using CrossCam.CustomElement;
using CrossCam.Droid.CustomRenderer;
using CrossCam.Wrappers;
using Xamarin.Forms;
using Xamarin.Essentials;
using Debug = System.Diagnostics.Debug;

[assembly: Dependency(typeof(PlatformBluetooth))]
namespace CrossCam.Droid.CustomRenderer
{
    public sealed class PlatformBluetooth : BluetoothGattCallback, IPlatformBluetooth, GoogleApiClient.IConnectionCallbacks, GoogleApiClient.IOnConnectionFailedListener
    {
        public static TaskCompletionSource<bool> BluetoothPermissionsTask = new TaskCompletionSource<bool>();
        public static TaskCompletionSource<bool> LocationPermissionsTask = new TaskCompletionSource<bool>();

        private static TaskCompletionSource<bool> _isLocationOnTask = new TaskCompletionSource<bool>();

        private readonly GoogleApiClient _googleApiClient;
        private string _connectedPartnerId;

        public PlatformBluetooth()
        {
            _googleApiClient = new GoogleApiClient.Builder(MainActivity.Instance)
                .AddConnectionCallbacks(this)
                .AddOnConnectionFailedListener(this)
                .AddApi(NearbyClass.CONNECTIONS_API)
                .Build(); 
            _googleApiClient.Connect();
        }

        public void Disconnect()
        {
            Debug.WriteLine("### Disconnecting");
            if (!string.IsNullOrWhiteSpace(_connectedPartnerId))
            {
                NearbyClass.Connections.DisconnectFromEndpoint(_googleApiClient, _connectedPartnerId);
            }
            NearbyClass.Connections.StopDiscovery(_googleApiClient);
            NearbyClass.Connections.StopAdvertising(_googleApiClient);
            NearbyClass.Connections.StopAllEndpoints(_googleApiClient);
            OnDisconnected();
        }

        private static Task<bool> RequestLocationPermissions()
        {
            LocationPermissionsTask = new TaskCompletionSource<bool>();
            MainActivity.Instance.CheckForAndRequestLocationPermissions();
            return LocationPermissionsTask.Task;
        }

        private static Task<bool> TurnOnLocationServices()
        {
            return CheckForAndTurnOnLocationServices();
        }

        public static Task<bool> CheckForAndTurnOnLocationServices(bool checkOnly = false)
        {
            //Debug.WriteLine("### DoingLocationStuff");
            if (!checkOnly)
            {
                _isLocationOnTask = new TaskCompletionSource<bool>();
            }

            var googleApiClient =
                new GoogleApiClient.Builder(MainActivity.Instance).AddApi(LocationServices.API).Build();
            googleApiClient.Connect();

            var builder = new LocationSettingsRequest.Builder().AddLocationRequest(new LocationRequest());
            builder.SetAlwaysShow(true);

            var result = LocationServices.SettingsApi.CheckLocationSettings(googleApiClient, builder.Build());
            result.SetResultCallback((LocationSettingsResult callback) =>
            {
                switch (callback.Status.StatusCode)
                {
                    case CommonStatusCodes.Success:
                    {
                        _isLocationOnTask.SetResult(true);
                        break;
                    }
                    case CommonStatusCodes.ResolutionRequired:
                    {
                        if (!checkOnly)
                        {
                            try
                            {
                                callback.Status.StartResolutionForResult(MainActivity.Instance,
                                    (int) MainActivity.RequestCodes.TurnLocationServicesOnRequestCode);
                            }
                            catch (IntentSender.SendIntentException e)
                            {
                                _isLocationOnTask.SetResult(false);
                            }
                        }
                        else
                        {
                            _isLocationOnTask.SetResult(false);
                        }

                        break;
                    }
                    default:
                    {
                        if (!checkOnly)
                        {
                            MainActivity.Instance.StartActivity(new Intent(Android.Provider.Settings
                                .ActionLocationSourceSettings));
                        }
                        else
                        {
                            _isLocationOnTask.SetResult(false);
                        }
                        break;
                    }
                }
            });

            return _isLocationOnTask.Task;
        }

        public async void SendPayload(byte[] bytes)
        {
            try
            {
                var sendingStream = new MemoryStream(bytes); //TODO: dispose/close correctly - i keep doing it wrong
                if ((BluetoothOperator.CrossCommand)bytes[2] == BluetoothOperator.CrossCommand.CapturedImage)
                {
                    await Task.Delay(1000);
                }
                await NearbyClass.Connections.SendPayloadAsync(_googleApiClient, _connectedPartnerId,
                    Payload.FromStream(sendingStream));
            }
            catch (Exception e)
            {
                Debug.WriteLine("### EXCEPTION SENDING: " + e);
            }
        }

        public event EventHandler<byte[]> PayloadReceived;
        private void ProcessReceivedPayload(byte[] payload)
        {
            var handler = PayloadReceived;
            handler?.Invoke(this, payload);
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

        public void OnConnected(Bundle connectionHint)
        {
            //Debug.WriteLine("### OnConnected " + (connectionHint != null ? string.Join(",", connectionHint.KeySet()) : ""));
        }

        public void OnConnectionSuspended(int cause)
        {
            //Debug.WriteLine("### OnConnectionSuspended " + cause);
        }

        public void OnConnectionFailed(ConnectionResult result)
        {
            //Debug.WriteLine("### OnConnectionFailed " + result.ErrorMessage);
        }

        public async Task<bool> BecomeDiscoverable()
        {
            //Debug.WriteLine("### BecomingDiscoverable");
            var result = await NearbyClass.Connections.StartAdvertisingAsync(_googleApiClient, DeviceInfo.Name, BluetoothOperator.CROSSCAM_SERVICE,
                new MyConnectionLifecycleCallback(this), new AdvertisingOptions.Builder().SetStrategy(Strategy.P2pPointToPoint).Build());
            return result.Status.IsSuccess;
        }

        public async Task<string> StartScanning()
        {
            //Debug.WriteLine("### StartingScanning");
            await RequestLocationPermissions();
            await TurnOnLocationServices();

            var statuses = await NearbyClass.Connections.StartDiscoveryAsync(_googleApiClient,
            BluetoothOperator.CROSSCAM_SERVICE, new MyEndpointDiscoveryCallback(this),
            new DiscoveryOptions.Builder().SetStrategy(Strategy.P2pPointToPoint).Build());

            return statuses.IsSuccess ? null : statuses.StatusMessage;
        }

        private class MyConnectionLifecycleCallback : ConnectionLifecycleCallback
        {
            private readonly PlatformBluetooth _platformBluetooth;

            public MyConnectionLifecycleCallback(PlatformBluetooth platformBluetooth)
            {
                _platformBluetooth = platformBluetooth;
            }

            public override void OnConnectionInitiated(string p0, ConnectionInfo p1)
            {
                Debug.WriteLine("### OnConnectionInitiated: " + p0 + ", " + p1.EndpointName);
                new AlertDialog.Builder(MainActivity.Instance).SetTitle("Accept connection to " + p1.EndpointName + "?")
                    .SetMessage("Confirm the code matches on both devices: " + p1.AuthenticationToken)
                    .SetPositiveButton("Accept",
                        (sender, args) =>
                        {
                            NearbyClass.Connections.AcceptConnection(_platformBluetooth._googleApiClient, p0,
                                new MyPayloadCallback(_platformBluetooth));
                        }).SetNegativeButton("Cancel",
                        (sender, args) =>
                        {
                            NearbyClass.Connections.RejectConnection(_platformBluetooth._googleApiClient, p0);
                            _platformBluetooth.Disconnect();
                            _platformBluetooth.OnDisconnected();
                        }).Show();
            }

            public override void OnConnectionResult(string p0, ConnectionResolution p1)
            {
                //Debug.WriteLine("### OnConnectionResult " + p0 + ", " + p1.Status.StatusMessage);
                if (p1.Status.IsSuccess)
                {
                    _platformBluetooth._connectedPartnerId = p0;
                    _platformBluetooth.OnConnected();
                    NearbyClass.Connections.StopDiscovery(_platformBluetooth._googleApiClient);
                    NearbyClass.Connections.StopAdvertising(_platformBluetooth._googleApiClient);
                }
                else
                {
                    _platformBluetooth.Disconnect();
                    _platformBluetooth.OnDisconnected();
                }
            }

            public override void OnDisconnected(string p0)
            {
                Debug.WriteLine("### OnDisconnected " + p0);
                _platformBluetooth.OnDisconnected();
            }
        }

        private class MyPayloadCallback : PayloadCallback
        {
            private readonly PlatformBluetooth _platformBluetooth;
            private Payload _mostRecentReceivedPayload;
            private byte[] _incomingBytes;
            private int _incomingBytesCounter;
            private int _expectedLength;
            private readonly byte[] _headerBytes = new byte[BluetoothOperator.HEADER_LENGTH];

            public MyPayloadCallback(PlatformBluetooth platformBluetooth)
            {
                _platformBluetooth = platformBluetooth;
            }

            //TODO: handle closing and disposing better, what's with the "failed to call close" stuff?
            //TODO: consider a constant sized buffer
            public override void OnPayloadReceived(string p0, Payload p1)
            {
                //Debug.WriteLine("### OnPayloadReceived, Id: " + p1.Id);
                _mostRecentReceivedPayload = p1;

                try
                {
                    var receivingStream = _mostRecentReceivedPayload.AsStream().AsInputStream(); //TODO: dispose/close correctly - i keep doing it wrong
                    for (var ii = 0; ii < BluetoothOperator.HEADER_LENGTH; ii++)
                    {
                        var nextByte = receivingStream.ReadByte();
                        _headerBytes[ii] = (byte)nextByte;
                    }
                    _expectedLength = BluetoothOperator.HEADER_LENGTH + ((_headerBytes.ElementAt(3) << 16) | (_headerBytes.ElementAt(4) << 8) | _headerBytes.ElementAt(5));

                    _incomingBytes = new byte[_expectedLength];
                    _incomingBytesCounter = BluetoothOperator.HEADER_LENGTH;
                    for (var ii = 0; ii < _headerBytes.Length; ii++)
                    {
                        _incomingBytes[ii] = _headerBytes[ii];
                    }

                    var readBytes = receivingStream.Read(_incomingBytes, _incomingBytesCounter, _expectedLength - _incomingBytesCounter);
                    _incomingBytesCounter += readBytes;
                }
                catch (Exception e)
                {
                    Debug.WriteLine("### EXCEPTION UPDATING: " + e);
                }
            }

            public override void OnPayloadTransferUpdate(string p0, PayloadTransferUpdate p1)
            {
                //Debug.WriteLine("### OnPayloadTransferUpdate " + (_mostRecentReceivedPayload?.Id == p1.PayloadId ? "RECEIVE" : "SEND") + " Id: " + p1.PayloadId + " status: " + p1.TransferStatus + " progress: " + p1.BytesTransferred + "/" + _expectedLength);
                if (p1.PayloadId == _mostRecentReceivedPayload?.Id) //only care about receiving
                {
                    var receivingStream = _mostRecentReceivedPayload.AsStream().AsInputStream(); //TODO: dispose/close correctly - i keep doing it wrong
                    int readBytes;
                    switch (p1.TransferStatus)
                    {
                        case PayloadTransferUpdate.Status.InProgress:
                            readBytes = receivingStream.Read(_incomingBytes, _incomingBytesCounter,
                                _expectedLength - _incomingBytesCounter);
                            _incomingBytesCounter += readBytes;
                            break;
                        case PayloadTransferUpdate.Status.Success:
                            readBytes = receivingStream.Read(_incomingBytes, _incomingBytesCounter,
                                _expectedLength - _incomingBytesCounter);
                            _incomingBytesCounter += readBytes;

                            //Debug.WriteLine("### ProcessReceivedPayload: " + _incomingBytesCounter + " of " + _expectedLength + ", Id: " + p1.PayloadId);
                            _platformBluetooth.ProcessReceivedPayload(_incomingBytes);
                            break;
                        default: //failure
                            Debug.WriteLine("### Transfer failed!");
                            break;
                    }
                }
                else
                {
                    if (p1.TransferStatus == PayloadTransferUpdate.Status.Success)
                    {
                        //Debug.WriteLine("### Closing due to sent success");
                    }
                }
            }
        }

        private class MyEndpointDiscoveryCallback : EndpointDiscoveryCallback
        {
            private readonly PlatformBluetooth _bluetooth;

            public MyEndpointDiscoveryCallback(PlatformBluetooth bluetooth)
            {
                _bluetooth = bluetooth;
            }

            public override async void OnEndpointFound(string p0, DiscoveredEndpointInfo p1)
            {
                Debug.WriteLine("### OnEndpointFound " + p0 + ", " + p1.EndpointName);
                if (p1.ServiceId == BluetoothOperator.CROSSCAM_SERVICE)
                {
                    await NearbyClass.Connections.RequestConnectionAsync(_bluetooth._googleApiClient, DeviceInfo.Name,
                        p0, new MyConnectionLifecycleCallback(_bluetooth));
                }
            }

            public override void OnEndpointLost(string p0)
            {
                //Debug.WriteLine("### OnEndpointLost " + p0);
            }
        }
    }
}