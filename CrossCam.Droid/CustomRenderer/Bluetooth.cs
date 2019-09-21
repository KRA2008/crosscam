using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Android.Bluetooth;
using Android.Content;
using CrossCam.Droid.CustomRenderer;
using CrossCam.Wrappers;
using Java.Util;
using Xamarin.Forms;
using Application = Android.App.Application;

[assembly: Dependency(typeof(Bluetooth))]
namespace CrossCam.Droid.CustomRenderer
{
    public sealed class Bluetooth : IBluetooth
    {
        public static TaskCompletionSource<bool> BluetoothPermissionsTask = new TaskCompletionSource<bool>();
        public static TaskCompletionSource<bool> IsBluetoothOnSource = new TaskCompletionSource<bool>();
        public static TaskCompletionSource<bool> IsDeviceDiscoverableSource = new TaskCompletionSource<bool>();
        public static readonly ObservableCollection<BluetoothDevice> AvailableDevices = new ObservableCollection<BluetoothDevice>();
        private BluetoothSocket _bluetoothSocket;

        public Bluetooth()
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

        public Task<bool> RequestBluetoothPermissions()
        {
            BluetoothPermissionsTask = new TaskCompletionSource<bool>();
            MainActivity.Instance.CheckForAndRequestBluetoothPermissions();
            return BluetoothPermissionsTask.Task;
        }

        public bool IsBluetoothSupported()
        {
            return BluetoothAdapter.DefaultAdapter != null;
        }

        public Task<bool> TurnOnBluetooth()
        {
            IsBluetoothOnSource = new TaskCompletionSource<bool>();
            if (!BluetoothAdapter.DefaultAdapter.IsEnabled)
            {
                MainActivity.Instance.StartActivityForResult(new Intent(BluetoothAdapter.ActionRequestEnable),
                    (int)MainActivity.RequestCodes.TurnOnBluetoothRequestCode);
                return IsBluetoothOnSource.Task;
            }

            return Task.FromResult(true);
        }

        public List<PartnerDevice> GetPairedDevices()
        {
            return BluetoothAdapter.DefaultAdapter.BondedDevices
                .Select(device => new PartnerDevice { Name = device.Name ?? "Unnamed", Address = device.Address }).ToList();
        }

        public bool BeginSearchForDiscoverableDevices()
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
            IsDeviceDiscoverableSource = new TaskCompletionSource<bool>();
            MainActivity.Instance.StartActivityForResult(new Intent(BluetoothAdapter.ActionRequestDiscoverable),
                (int)MainActivity.RequestCodes.MakeBluetoothDiscoverableRequestCode);
            return IsDeviceDiscoverableSource.Task;
        }

        public async Task<bool> ListenForConnections()
        {
            try
            {
                var serverSocket =
                    BluetoothAdapter.DefaultAdapter.ListenUsingRfcommWithServiceRecord(Application.Context.PackageName,
                        UUID.FromString(PartnerDevice.SDP_UUID));
                _bluetoothSocket = await serverSocket.AcceptAsync();
                if (_bluetoothSocket != null)
                {
                    serverSocket.Close();
                    BluetoothAdapter.DefaultAdapter.CancelDiscovery();
                    return _bluetoothSocket.IsConnected;
                }
            }
            catch (Exception e)
            {
                BluetoothAdapter.DefaultAdapter.CancelDiscovery();
                return false;
            }

            BluetoothAdapter.DefaultAdapter.CancelDiscovery();
            return false;
        }

        public async Task<bool> AttemptConnection(PartnerDevice partnerDevice)
        {
            try
            {
                var targetDevice = AvailableDevices.FirstOrDefault(d => d.Address == partnerDevice.Address);
                if (targetDevice != null)
                {
                    _bluetoothSocket = targetDevice.CreateRfcommSocketToServiceRecord(UUID.FromString(PartnerDevice.SDP_UUID));
                    await _bluetoothSocket.ConnectAsync();
                    return _bluetoothSocket.IsConnected;
                }
            }
            catch (Exception e)
            {
                return false;
            }

            return false;
        }

        public void ForgetDevice(PartnerDevice partnerDevice)
        {
            var targetDevice = BluetoothAdapter.DefaultAdapter.BondedDevices.FirstOrDefault(d => d.Address == partnerDevice.Address);
            if (targetDevice != null)
            {
                var mi = targetDevice.Class.GetMethod("removeBond", null); 
                mi.Invoke(targetDevice, null);
            }
        }
    }
}