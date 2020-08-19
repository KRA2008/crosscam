using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Android.Bluetooth;
using Android.Content;
using Android.Gms.Common;
using Android.Gms.Common.Apis;
using Android.Gms.Location;
using Android.Gms.Nearby;
using Android.Gms.Nearby.Connection;
using Android.OS;
using CrossCam.CustomElement;
using CrossCam.Droid.CustomRenderer;
using CrossCam.Wrappers;
using Java.Util;
using Xamarin.Forms;
using Xamarin.Essentials;
using Debug = System.Diagnostics.Debug;
using File = Java.IO.File;

[assembly: Dependency(typeof(PlatformBluetooth))]
namespace CrossCam.Droid.CustomRenderer
{
    public sealed class PlatformBluetooth : BluetoothGattCallback, IPlatformBluetooth, GoogleApiClient.IConnectionCallbacks, GoogleApiClient.IOnConnectionFailedListener
    {
        public static TaskCompletionSource<bool> BluetoothPermissionsTask = new TaskCompletionSource<bool>();
        public static TaskCompletionSource<bool> LocationPermissionsTask = new TaskCompletionSource<bool>();
        public static TaskCompletionSource<bool> IsBluetoothOnTask = new TaskCompletionSource<bool>();
        public static TaskCompletionSource<bool> IsDeviceDiscoverableTask = new TaskCompletionSource<bool>();
        public static readonly ObservableCollection<BluetoothDevice> AvailableDevices =
            new ObservableCollection<BluetoothDevice>();

        private static TaskCompletionSource<bool> _isLocationOnTask = new TaskCompletionSource<bool>();
        private BluetoothSocket _bluetoothSocket;

        private readonly GoogleApiClient _googleApiClient;
        private string _partnerId;
        private bool _isSending;

        public PlatformBluetooth()
        {
            AvailableDevices.CollectionChanged += (sender, args) =>
            {
                if (args.Action == NotifyCollectionChangedAction.Add)
                {
                    foreach (var newItem in args.NewItems)
                    {
                        var newDevice = (BluetoothDevice)newItem;
                        OnDeviceDiscovered(new PartnerDevice
                        {
                            Name = newDevice.Name ?? "Unnamed",
                            Address = newDevice.Address
                        });
                    }
                }
            };

            _googleApiClient = new GoogleApiClient.Builder(MainActivity.Instance)
                .AddConnectionCallbacks(this)
                .AddOnConnectionFailedListener(this)
                .AddApi(NearbyClass.CONNECTIONS_API)
                .Build(); 
            _googleApiClient.Connect();
        }

        public bool IsPrimary { get; set; }

        public void Disconnect()
        {
            NearbyClass.Connections.DisconnectFromEndpoint(_googleApiClient, _partnerId);
        }

        public Task<bool> RequestBluetoothPermissions()
        {
            BluetoothPermissionsTask = new TaskCompletionSource<bool>();
            MainActivity.Instance.CheckForAndRequestBluetoothPermissions();
            return BluetoothPermissionsTask.Task;
        }

        public Task<bool> RequestLocationPermissions()
        {
            LocationPermissionsTask = new TaskCompletionSource<bool>();
            MainActivity.Instance.CheckForAndRequestLocationPermissions();
            return LocationPermissionsTask.Task;
        }

        public bool IsBluetoothSupported()
        {
            return BluetoothAdapter.DefaultAdapter != null;
        }

        public bool IsBluetoothApiLevelSufficient()
        {
            return Build.VERSION.SdkInt >= BuildVersionCodes.M;
        }

        public bool IsServerSupported()
        {
            return Build.VERSION.SdkInt >= BuildVersionCodes.M;
        }

        public Task<bool> TurnOnBluetooth()
        {
            IsBluetoothOnTask = new TaskCompletionSource<bool>();
            if (!BluetoothAdapter.DefaultAdapter.IsEnabled)
            {
                MainActivity.Instance.StartActivityForResult(new Intent(BluetoothAdapter.ActionRequestEnable),
                    (int)MainActivity.RequestCodes.TurnOnBluetoothRequestCode);
                return IsBluetoothOnTask.Task;
            }

            return Task.FromResult(true);
        }

        public Task<bool> TurnOnLocationServices()
        {
            return CheckForAndTurnOnLocationServices();
        }

        public static Task<bool> CheckForAndTurnOnLocationServices(bool checkOnly = false)
        {
            if (!checkOnly)
            {
                _isLocationOnTask = new TaskCompletionSource<bool>();
            }

            var googleApiClient =
                new GoogleApiClient.Builder(MainActivity.Instance).AddApi(LocationServices.API).Build();
            googleApiClient.Connect();

            var builder = new LocationSettingsRequest.Builder().AddLocationRequest(new LocationRequest());
            builder.SetAlwaysShow(true);
            builder.SetNeedBle(true);

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
                Debug.WriteLine("### SENDING: " + (BluetoothOperator.CrossCommand)bytes[2]);
                _isSending = true;
                var memoryStream = new MemoryStream(bytes);
                await NearbyClass.Connections.SendPayloadAsync(_googleApiClient, _partnerId,
                    Payload.FromStream(memoryStream));
                _isSending = false;
            }
            catch (Exception e)
            {
                Debug.WriteLine("### EXCEPTION SENDING: " + e);
            }
        }

        public event EventHandler<byte[]> PayloadReceived;
        private void OnPayloadReceived(byte[] payload)
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

        public event EventHandler<PartnerDevice> DeviceDiscovered;
        private void OnDeviceDiscovered(PartnerDevice e)
        {
            var handler = DeviceDiscovered;
            handler?.Invoke(this, e);
        }

        public async Task ListenForConnections()
        {
            var serverSocket =
                BluetoothAdapter.DefaultAdapter.ListenUsingRfcommWithServiceRecord(Android.App.Application.Context.PackageName,
                    UUID.FromString(BluetoothOperator.ServiceGuid.ToString()));
            try
            {
                _bluetoothSocket = await serverSocket.AcceptAsync();
            }
            catch (Exception e)
            {
                if (string.Equals(e.Message, "Try again", StringComparison.OrdinalIgnoreCase))
                {
                    //TODO: what was this for???
                    //if (_bluetoothSocket != null &&
                    //    _bluetoothSocket.IsConnected)
                    //{
                    //    serverSocket.Close();
                    //    BluetoothAdapter.DefaultAdapter.CancelDiscovery();
                    //    OnConnected();
                    //    return true;
                    //}

                    //return false;
                }

                throw;
            }

            if (_bluetoothSocket != null)
            {
                if (_bluetoothSocket.IsConnected)
                {
                    IsPrimary = false;
                    OnConnected();
                } //TODO: what if it fails?
            }

            serverSocket.Close();
            BluetoothAdapter.DefaultAdapter.CancelDiscovery();
        }

        public async Task AttemptConnection(PartnerDevice partnerDevice)
        {
            var didConnect = false;
            var targetDevice = BluetoothAdapter.DefaultAdapter.BondedDevices.Union(AvailableDevices)
                .FirstOrDefault(d => d.Address == partnerDevice.Address);
            if (targetDevice != null)
            {
                _bluetoothSocket =
                    targetDevice.CreateRfcommSocketToServiceRecord(UUID.FromString(BluetoothOperator.ServiceGuid.ToString()));
                await _bluetoothSocket.ConnectAsync();
                if (_bluetoothSocket.IsConnected)
                {
                    IsPrimary = true;
                    OnConnected();
                } //TODO: what if it fails?
            }
        }

        public void ForgetDevice(PartnerDevice partnerDevice)
        {
            var targetDevice =
                BluetoothAdapter.DefaultAdapter.BondedDevices.FirstOrDefault(d => d.Address == partnerDevice.Address);
            if (targetDevice != null)
            {
                var mi = targetDevice.Class.GetMethod("removeBond", null);
                mi.Invoke(targetDevice, null);
            }
        }

        public void OnConnected(Bundle connectionHint)
        {
            Debug.WriteLine("### OnConnected " + connectionHint);
        }

        public void OnConnectionSuspended(int cause)
        {
            Debug.WriteLine("### OnConnectionSuspended " + cause);
        }

        public void OnConnectionFailed(ConnectionResult result)
        {
            Debug.WriteLine("### OnConnectionFailed " + result.ErrorMessage);
        }

        public async Task<bool> BecomeDiscoverable()
        {
            var result = await NearbyClass.Connections.StartAdvertisingAsync(_googleApiClient, DeviceInfo.Name, BluetoothOperator.CROSSCAM_SERVICE,
                new MyConnectionLifecycleCallback(this), new AdvertisingOptions.Builder().SetStrategy(Strategy.P2pPointToPoint).Build());
            return result.Status.IsSuccess;
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
                Debug.WriteLine("### OnConnectionInitiated " + p0 + ", " + p1.EndpointName + ", Incoming: " + p1.IsIncomingConnection);
                //if (p1.IsIncomingConnection)
                //{
                    NearbyClass.Connections.AcceptConnection(_platformBluetooth._googleApiClient, p0, new MyPayloadCallback(_platformBluetooth));
                    //if (result..IsSuccess)
                    //{
                    //    Debug.WriteLine("### AcceptConnectionAsync result: " + p0 + ", " + result.StatusMessage);
                    //    _platformBluetooth._partnerId = p0;
                    //    _platformBluetooth.OnConnected();
                    //    NearbyClass.Connections.StopDiscovery(_platformBluetooth._googleApiClient);
                    //    NearbyClass.Connections.StopAdvertising(_platformBluetooth._googleApiClient);
                    //}
                //}
            }

            public override void OnConnectionResult(string p0, ConnectionResolution p1)
            {
                Debug.WriteLine("### OnConnectionResult " + p0 + ", " + p1.Status.StatusMessage);
                if (p1.Status.IsSuccess)
                {
                    _platformBluetooth._partnerId = p0;
                    _platformBluetooth.OnConnected();
                    NearbyClass.Connections.StopDiscovery(_platformBluetooth._googleApiClient);
                    NearbyClass.Connections.StopAdvertising(_platformBluetooth._googleApiClient);
                }
            }

            public override void OnDisconnected(string p0)
            {
                Debug.WriteLine("### OnDisconnected " + p0);
            }
        }

        private class MyPayloadCallback : PayloadCallback
        {
            private readonly PlatformBluetooth _platformBluetooth;
            private Payload _mostRecentPayload;
            private byte[] _incomingBytes;
            private long _incomingBytesCounter;
            private bool _isHeaderRead;
            private long _expectedLength;
            private readonly byte[] _headerBytes = new byte[BluetoothOperator.HEADER_LENGTH];

            public MyPayloadCallback(PlatformBluetooth platformBluetooth)
            {
                _platformBluetooth = platformBluetooth;
            }

            public override void OnPayloadReceived(string p0, Payload p1)
            {
                //Debug.WriteLine("### OnPayloadReceived, Id: " + p1.Id);
                if (!_platformBluetooth._isSending)
                {
                    _mostRecentPayload = p1;
                    _isHeaderRead = false;
                }
            }

            public override void OnPayloadTransferUpdate(string p0, PayloadTransferUpdate p1)
            {
                //Debug.WriteLine("### OnPayloadTransferUpdate, Id: " + p1.PayloadId + " status: " + p1.TransferStatus + " sending: " + _platformBluetooth._isSending);
                if (!_platformBluetooth._isSending &&
                    _mostRecentPayload != null)
                {
                    try
                    {
                        int nextByte;
                        var memoryStream = _mostRecentPayload.AsStream().AsInputStream();

                        if (!_isHeaderRead)
                        {
                            for (var ii = 0; ii < BluetoothOperator.HEADER_LENGTH; ii++)
                            {
                                 nextByte = memoryStream.ReadByte();
                                _headerBytes[ii] = (byte)nextByte;
                            }
                            _expectedLength = BluetoothOperator.HEADER_LENGTH + (_headerBytes.ElementAt(3) << 16) | (_headerBytes.ElementAt(4) << 8) | _headerBytes.ElementAt(5);
                            Debug.WriteLine("### RECEIVING: " + (BluetoothOperator.CrossCommand)_headerBytes[2]);

                            if (_expectedLength == 0)
                            {
                                _platformBluetooth.OnPayloadReceived(_headerBytes);
                                memoryStream.Close();
                                _mostRecentPayload = null;
                                return;
                            }

                            _incomingBytes = new byte[_expectedLength];
                            _incomingBytesCounter = BluetoothOperator.HEADER_LENGTH;
                            for (var ii = 0; ii < _headerBytes.Length; ii++)
                            {
                                _incomingBytes[ii] = _headerBytes[ii];
                            }

                            _isHeaderRead = true;
                        }

                        while (_incomingBytesCounter < _expectedLength)
                        {
                            nextByte = memoryStream.ReadByte();
                            _incomingBytes[_incomingBytesCounter] = (byte) nextByte;
                            _incomingBytesCounter++;
                        }

                        if (_expectedLength == _incomingBytes.Length)
                        {
                            _platformBluetooth.OnPayloadReceived(_incomingBytes);
                            memoryStream.Close();
                            _mostRecentPayload = null;
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine("### EXCEPTION UPDATING: " + e);
                    }
                }
            }
        }

        public async Task<bool> StartScanning()
        {
            await RequestLocationPermissions();
            var result = await NearbyClass.Connections.StartDiscoveryAsync(_googleApiClient,
                BluetoothOperator.CROSSCAM_SERVICE, new MyEndpointDiscoveryCallback(this),
                new DiscoveryOptions.Builder().SetStrategy(Strategy.P2pPointToPoint).Build());
            return result.Status.IsSuccess;
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
                Debug.WriteLine("### OnEndpointLost " + p0);
            }
        }
    }
}