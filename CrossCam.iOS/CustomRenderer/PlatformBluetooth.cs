using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CoreBluetooth;
using CrossCam.CustomElement;
using CrossCam.iOS.CustomRenderer;
using CrossCam.Wrappers;
using ExternalAccessory;
using Foundation;
using Xamarin.Forms;

[assembly: Dependency(typeof(PlatformBluetooth))]
namespace CrossCam.iOS.CustomRenderer
{
    public class PlatformBluetooth : IPlatformBluetooth
    {
        public event EventHandler<PartnerDevice> DeviceDiscovered;
        private const string PROTOCOL = "com.kra2008.crosscam";

        private readonly ObservableCollection<EAAccessory> _availableDevices =
            new ObservableCollection<EAAccessory>();

        private TaskCompletionSource<bool?> _secondaryConnectionCompletionSource;

        private EASession _connection;

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

        private void OnDeviceDiscovered(PartnerDevice e)
        {
            var handler = DeviceDiscovered;
            handler?.Invoke(this, e);
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

        public IEnumerable<PartnerDevice> GetPairedDevices()
        {
            return Enumerable.Empty<PartnerDevice>().ToList();
        }

        public bool StartScanning()
        {
            var connectedAccessories = EAAccessoryManager.SharedAccessoryManager.ConnectedAccessories;
            EAAccessoryManager.Notifications.ObserveDidConnect(EAAccessoryConnected);
            EAAccessoryManager.Notifications.ObserveDidDisconnect(EAAccessoryDisconnected);
            EAAccessoryManager.SharedAccessoryManager.RegisterForLocalNotifications();

            foreach (var accessory in connectedAccessories)
            {
                foreach (var protocolString in accessory.ProtocolStrings)
                {
                    if (protocolString.Contains(PROTOCOL))
                    {
                        _availableDevices.Add(accessory);
                    }
                    break;
                }
            }
            return true;
        }

        private void EAAccessoryDisconnected(object sender, EAAccessoryEventArgs e)
        {
            Debug.WriteLine("Accessory disconnected says manager: " + e.Accessory.Name);
        }

        private void EAAccessoryConnected(object sender, EAAccessoryEventArgs e)
        {
            Debug.WriteLine("Accessory connected says manager: " + e.Accessory.Name);
        }

        public void ForgetDevice(PartnerDevice partnerDevice)
        {
        }

        public async Task<bool> BecomeDiscoverable()
        {
            await EAAccessoryManager.SharedAccessoryManager.ShowBluetoothAccessoryPickerAsync(null);

            var serviceUuid = CBUUID.FromString(BluetoothOperator.ServiceGuid.ToString());
            return true;
        }

        public Task<bool?> ListenForConnections()
        {
            return Task.FromResult((bool?)null);
        }

        public Task<bool> SayHello()
        {
            return Task.FromResult(true);
        }

        public Task<bool> ListenForHello()
        {
            return Task.FromResult(true);
        }

        public Task<bool> AttemptConnection(PartnerDevice partnerDevice)
        {
            var selectedDevice = _availableDevices.FirstOrDefault(d => d.Name == partnerDevice.Name);
            if (selectedDevice != null)
            {
                _connection = new EASession(selectedDevice, PROTOCOL);
                _connection.Accessory.Disconnected += delegate
                {
                    //TODO: close session
                };
                _connection.Accessory.Delegate = new AccessoryDelegate();

                _connection.InputStream.Schedule(NSRunLoop.Current, NSRunLoop.NSDefaultRunLoopMode);
                _connection.InputStream.Open();

                _connection.OutputStream.Schedule(NSRunLoop.Current, NSRunLoop.NSDefaultRunLoopMode);
                _connection.OutputStream.Open();
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public bool IsServerSupported()
        {
            return true;
        }

        public bool IsBluetoothApiLevelSufficient()
        {
            return true;
        }

        public Task<bool> SendPreviewFrame(byte[] preview)
        {
            throw new NotImplementedException();
        }

        public Task<byte[]> Capture(int countdownSeconds)
        {
            throw new NotImplementedException();
        }

        private class AccessoryDelegate : EAAccessoryDelegate
        {
            public override void Disconnected(EAAccessory accessory)
            {
                Debug.WriteLine("Disconnected in delegate " + accessory.Name);
            }
        }
    }
}