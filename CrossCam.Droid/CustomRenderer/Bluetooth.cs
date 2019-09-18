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
    public class Bluetooth : IBluetooth
    {
        public static TaskCompletionSource<bool> IsBluetoothOnSource;
        public static TaskCompletionSource<bool> IsDeviceDiscoverableSource;
        public static readonly ObservableCollection<BluetoothDevice> AvailableDevices = new ObservableCollection<BluetoothDevice>();
        private ObservableCollection<PartnerDevice> _displayAvailableDevices;
        private BluetoothSocket _bluetoothSocket;
        private const string SDP_UUID = "492a8e3d-2589-40b1-b9c2-419a7ce80f3c";

        public Bluetooth()
        {
            AvailableDevices.CollectionChanged += (sender, args) =>
            {
                if (args.Action == NotifyCollectionChangedAction.Add)
                {
                    foreach (var newItem in args.NewItems)
                    {
                        var newDevice = (BluetoothDevice) newItem;
                        _displayAvailableDevices.Add(new PartnerDevice
                        {
                            Name = newDevice.Name ?? "Unnamed",
                            Address = newDevice.Address
                        });
                    }
                }
            };
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
                .Select(device => new PartnerDevice {Name = device.Name, Address = device.Address}).ToList();
        }

        public bool SearchForAvailableDevices(ObservableCollection<PartnerDevice> partnerDevices)
        {
            AvailableDevices.Clear();
            _displayAvailableDevices = partnerDevices;
            return BluetoothAdapter.DefaultAdapter.StartDiscovery();
        }

        public async Task<bool> ListenForConnections()
        {
            try
            {
                IsDeviceDiscoverableSource = new TaskCompletionSource<bool>();
                MainActivity.Instance.StartActivityForResult(new Intent(BluetoothAdapter.ActionRequestDiscoverable),
                    (int)MainActivity.RequestCodes.MakeBluetoothDiscoverableRequestCode);
                if (await IsDeviceDiscoverableSource.Task)
                {
                    var serverSocket =
                        BluetoothAdapter.DefaultAdapter.ListenUsingRfcommWithServiceRecord(Application.Context.PackageName,
                            UUID.FromString(SDP_UUID));
                    _bluetoothSocket = await serverSocket.AcceptAsync();
                    if (_bluetoothSocket != null)
                    {
                        serverSocket.Close();
                        return _bluetoothSocket.IsConnected;
                    }
                }
            }
            catch (Exception e)
            {
                return false;
            }

            return false;
        }

        public async Task<bool> AttemptConnection(PartnerDevice partnerDevice)
        {
            try
            {
                var targetDevice = AvailableDevices.FirstOrDefault(d => d.Address == partnerDevice.Address);
                if (targetDevice != null)
                {
                    _bluetoothSocket = targetDevice.CreateRfcommSocketToServiceRecord(UUID.FromString(SDP_UUID));
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
    }
}