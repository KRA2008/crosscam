using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Android.Bluetooth;
using Android.Content;
using Android.Gms.Common.Apis;
using Android.Gms.Location;
using Android.OS;
using CrossCam.Droid.CustomRenderer;
using CrossCam.Wrappers;
using Java.Util;
using Xamarin.Forms;

[assembly: Dependency(typeof(PlatformBluetooth))]
namespace CrossCam.Droid.CustomRenderer
{
    public sealed class PlatformBluetooth : IPlatformBluetooth
    {
        public static TaskCompletionSource<bool> BluetoothPermissionsTask = new TaskCompletionSource<bool>();
        public static TaskCompletionSource<bool> LocationPermissionsTask = new TaskCompletionSource<bool>();
        public static TaskCompletionSource<bool> IsBluetoothOnTask = new TaskCompletionSource<bool>();
        public static TaskCompletionSource<bool> IsDeviceDiscoverableTask = new TaskCompletionSource<bool>();
        public static readonly ObservableCollection<BluetoothDevice> AvailableDevices =
            new ObservableCollection<BluetoothDevice>();

        private static TaskCompletionSource<bool> _isLocationOnTask = new TaskCompletionSource<bool>();
        private BluetoothSocket _bluetoothSocket;

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
        }

        public bool IsConnected()
        {
            return _bluetoothSocket != null &&
                   _bluetoothSocket.IsConnected;
        }

        public void Disconnect()
        {
            _bluetoothSocket?.Close();
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

        public IEnumerable<PartnerDevice> GetPairedDevices()
        {
            return BluetoothAdapter.DefaultAdapter.BondedDevices
                .Select(device => new PartnerDevice { Name = device.Name ?? "Unnamed", Address = device.Address })
                .ToList();
        }

        public bool StartScanning()
        {
            AvailableDevices.Clear();
            return BluetoothAdapter.DefaultAdapter.StartDiscovery();
        }

        public event EventHandler<PartnerDevice> DeviceDiscovered;
        private void OnDeviceDiscovered(PartnerDevice e)
        {
            var handler = DeviceDiscovered;
            handler?.Invoke(this, e);
        }

        public Task<bool> BecomeDiscoverable()
        {
            IsDeviceDiscoverableTask = new TaskCompletionSource<bool>();
            MainActivity.Instance.StartActivityForResult(new Intent(BluetoothAdapter.ActionRequestDiscoverable),
                (int)MainActivity.RequestCodes.MakeBluetoothDiscoverableRequestCode);
            return IsDeviceDiscoverableTask.Task;
        }

        public async Task<bool?> ListenForConnections()
        {
            var serverSocket =
                BluetoothAdapter.DefaultAdapter.ListenUsingRfcommWithServiceRecord(Android.App.Application.Context.PackageName,
                    UUID.FromString(PartnerDevice.SDP_UUID));
            try
            {
                _bluetoothSocket = await serverSocket.AcceptAsync();
            }
            catch (Exception e)
            {
                if (string.Equals(e.Message, "Try again", StringComparison.OrdinalIgnoreCase))
                {
                    if (_bluetoothSocket != null &&
                        _bluetoothSocket.IsConnected)
                    {
                        serverSocket.Close();
                        BluetoothAdapter.DefaultAdapter.CancelDiscovery();
                        return null;
                    }

                    return false;
                }

                throw;
            }

            if (_bluetoothSocket != null)
            {
                serverSocket.Close();
                BluetoothAdapter.DefaultAdapter.CancelDiscovery();
                return _bluetoothSocket.IsConnected;
            }

            serverSocket.Close();
            BluetoothAdapter.DefaultAdapter.CancelDiscovery();
            return false;
        }

        public Task<bool> SayHello()
        {
            if (_bluetoothSocket?.IsConnected == true)
            {
                var bytes = 
                await _bluetoothSocket.OutputStream.WriteAsync()
            }
        }

        public Task<bool> ListenForHello()
        {
            throw new NotImplementedException();
        }

        public async Task<bool> AttemptConnection(PartnerDevice partnerDevice)
        {
            var didConnect = false;
            var targetDevice = BluetoothAdapter.DefaultAdapter.BondedDevices.Union(AvailableDevices)
                .FirstOrDefault(d => d.Address == partnerDevice.Address);
            if (targetDevice != null)
            {
                _bluetoothSocket =
                    targetDevice.CreateRfcommSocketToServiceRecord(UUID.FromString(PartnerDevice.SDP_UUID));
                await _bluetoothSocket.ConnectAsync();
                didConnect = _bluetoothSocket.IsConnected;
            }

            return didConnect;
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

        public Task<bool> SendPreviewFrame(byte[] preview)
        {
            throw new NotImplementedException();
        }

        public Task<byte[]> Capture(int countdownSeconds)
        {
            throw new NotImplementedException();
        }
    }
}