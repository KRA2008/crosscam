using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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

        public override void OnCharacteristicWrite(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic, GattStatus status)
        {
            base.OnCharacteristicWrite(gatt, characteristic, status);
            //characteristic.Uuid
        }

        public bool IsPrimary { get; set; }

        public void Disconnect()
        {
            //_bluetoothSocket?.Close();
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

        public Task SendCaptue(byte[] capturedImage)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<PartnerDevice> GetPairedDevices()
        {
            return BluetoothAdapter.DefaultAdapter.BondedDevices
                .Select(device => new PartnerDevice { Name = device.Name ?? "Unnamed", Address = device.Address })
                .ToList();
        }

        public void SendSecondaryErrorOccurred()
        {
            throw new NotImplementedException();
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
        private void OnHelloReceived(double fov)
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

        public event EventHandler<PartnerDevice> DeviceDiscovered;
        private void OnDeviceDiscovered(PartnerDevice e)
        {
            var handler = DeviceDiscovered;
            handler?.Invoke(this, e);
        }

        public event EventHandler<byte[]> PreviewFrameReceived;
        public event EventHandler<long> ClockReadingReceived;
        public event EventHandler<DateTime> SyncReceived;
        public event EventHandler<byte[]> CaptureReceived;
        public event EventHandler SecondaryErrorReceived;

        private void OnPreviewFrameReceived(PartnerDevice e)
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

        public async Task<bool> SendFov(double fov)
        {
            var result = await NearbyClass.Connections.SendPayloadAsync(_googleApiClient, _partnerId,
                Payload.FromBytes(new[] {(byte) 0}));
            return result.IsSuccess;
            //if (_bluetoothSocket?.IsConnected == true)
            //{
            //    var bytes = Encoding.UTF8.GetBytes("Hi there friend.");
            //    await _bluetoothSocket.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            //    return true;
            //}

            //return false;
        }

        public async Task<bool> ListenForFov()
        {
            const int BUFFER_LENGTH = 1024;
            var buffer = new byte[BUFFER_LENGTH];
            var isMoreDataToRead = true;
            var helloBytes = new List<byte>();

            do
            {
                var read = await _bluetoothSocket.InputStream.ReadAsync(buffer, 0, buffer.Length);

                if (read < BUFFER_LENGTH)
                {
                    isMoreDataToRead = false;
                }

                if (read > 0)
                {
                    for (var ii = 0; ii < read; ii++)
                    {
                        helloBytes.Add(buffer[ii]);
                    }
                }

            } while (isMoreDataToRead);

            var helloByteArray = helloBytes.ToArray();
            var hello = Encoding.UTF8.GetString(helloByteArray, 0, helloByteArray.Length);
            Debug.WriteLine("Hello received: " + hello);
            return true;
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

        public Task SendReadyForPreviewFrame()
        {
            return Task.FromResult(true);
        }

        public Task SendClockReading()
        {
            return Task.FromResult(true);
        }

        public void ProcessClockReading(byte[] readingBytes)
        {
        }

        public Task SendSync(DateTime syncMoment)
        {
            return Task.FromResult(true);
        }

        public Task ProcessSyncAndCapture(byte[] syncBytes)
        {
            return Task.FromResult(true);
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

        public Task SendPreviewFrame(byte[] preview)
        {
            return Task.FromResult(true);
        }

        public Task SendReadyForClockReading()
        {
            return Task.FromResult(true);
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

            public override async void OnConnectionInitiated(string p0, ConnectionInfo p1)
            {
                Debug.WriteLine("### OnConnectionInitiated " + p0 + ", " + p1.EndpointName + ", Incoming: " + p1.IsIncomingConnection);
                //if (p1.IsIncomingConnection)
                //{
                    var result = await NearbyClass.Connections.AcceptConnectionAsync(_platformBluetooth._googleApiClient, p0, new MyPayloadCallback(_platformBluetooth));
                    if (result.IsSuccess)
                    {
                        Debug.WriteLine("### AcceptConnectionAsync result: " + p0 + ", " + result.StatusMessage);
                        _platformBluetooth._partnerId = p0;
                        _platformBluetooth.OnConnected();
                    }
                //}
            }

            public override void OnConnectionResult(string p0, ConnectionResolution p1)
            {
                Debug.WriteLine("### OnConnectionResult " + p0 + ", " + p1.Status.StatusMessage);
                if (p1.Status.IsSuccess)
                {
                    _platformBluetooth._partnerId = p0;
                    _platformBluetooth.OnConnected();
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

            public MyPayloadCallback(PlatformBluetooth platformBluetooth)
            {
                _platformBluetooth = platformBluetooth;
            }

            public override void OnPayloadReceived(string p0, Payload p1)
            {
                Debug.WriteLine("### OnPayloadReceived " + p0 + ", " + p1.AsBytes());
                _platformBluetooth.OnConnected();
            }

            public override void OnPayloadTransferUpdate(string p0, PayloadTransferUpdate p1)
            {
                Debug.WriteLine("### OnPayloadTransferUpdate " + p0 + ", " + p1.TransferStatus);
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