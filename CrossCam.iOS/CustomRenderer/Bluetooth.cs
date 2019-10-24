using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CoreBluetooth;
using CoreFoundation;
using CrossCam.iOS.CustomRenderer;
using CrossCam.Wrappers;
using Foundation;
using Xamarin.Forms;

[assembly: Dependency(typeof(Bluetooth))]
namespace CrossCam.iOS.CustomRenderer
{
    public class Bluetooth : IBluetooth
    {
        public static readonly ObservableCollection<CBPeripheral> AvailableDevices =
            new ObservableCollection<CBPeripheral>();

        private CBCentralManager _centralManager;

        public event EventHandler<PartnerDevice> DeviceDiscovered;

        public Bluetooth()
        {
            AvailableDevices.CollectionChanged += (sender, args) =>
            {
                if (args.Action == NotifyCollectionChangedAction.Add)
                {
                    foreach (var newItem in args.NewItems)
                    {
                        var newDevice = (CBPeripheral)newItem;
                        OnDeviceDiscovered(new PartnerDevice
                        {
                            Name = newDevice.Name ?? "Unnamed",
                            Address = "idk" //TODO
                        });
                    }
                }
            };
        }

        private void OnDeviceDiscovered(PartnerDevice e)
        {
            var handler = DeviceDiscovered;
            handler?.Invoke(this, e);
        }

        public bool IsConnected()
        {
            return false;
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

        public List<PartnerDevice> GetPairedDevices()
        {
            return Enumerable.Empty<PartnerDevice>().ToList();
        }

        public bool BeginSearchForDiscoverableDevices()
        {
            var managerDelegate = new BluetoothManagerDelegate();
            _centralManager = new CBCentralManager(managerDelegate, DispatchQueue.DefaultGlobalQueue,
                new CBCentralInitOptions());
            _centralManager.ScanForPeripherals(CBUUID.FromString(PartnerDevice.SDP_UUID));
            return false;
        }

        public Task<bool> BecomeDiscoverable()
        {
            return Task.FromResult(true);
        }

        public Task<bool?> ListenForConnections()
        {
            var taskCompletionSource = new TaskCompletionSource<bool?>();
            return taskCompletionSource.Task;
        }

        public Task<bool> AttemptConnection(PartnerDevice partnerDevice)
        {
            var matchingDevice = AvailableDevices.First(d => d.Name == partnerDevice.Name);
            _centralManager.ConnectPeripheral(matchingDevice);
            _centralManager.StopScan();
            return Task.FromResult(true);
        }

        public void ForgetDevice(PartnerDevice partnerDevice)
        {
        }

        public Task<bool> SendPreviewFrame(byte[] preview)
        {
            throw new NotImplementedException();
        }

        public Task<byte[]> Capture(int countdownSeconds)
        {
            throw new NotImplementedException();
        }

        private class BluetoothManagerDelegate : CBCentralManagerDelegate
        {
            public override void UpdatedState(CBCentralManager central)
            {
                Debug.WriteLine("Updated state: " + central.State);
            }

            public override void DiscoveredPeripheral(CBCentralManager central, CBPeripheral peripheral, NSDictionary advertisementData,
                NSNumber RSSI)
            {
                base.DiscoveredPeripheral(central, peripheral, advertisementData, RSSI);
                Debug.WriteLine("Discovered device: " + peripheral.Name);
                AvailableDevices.Add(peripheral);
                var peripheralDelegate = new BluetoothPeripheralDelgate();
                peripheral.Delegate = peripheralDelegate;
            }
        }

        private class BluetoothPeripheralDelgate : CBPeripheralDelegate
        {

        }
    }
}