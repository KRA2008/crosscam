using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CrossCam.Wrappers;
using FreshMvvm;
using Plugin.BluetoothLE;
using Plugin.BluetoothLE.Server;
using Xamarin.Forms;
using IDevice = Plugin.BluetoothLE.IDevice;

namespace CrossCam.ViewModel
{
    public class PairingViewModel : FreshBasePageModel
    {
        public bool IsConnected { get; set; }
        public ObservableCollection<IDevice> PairedDevices { get; set; }
        public ObservableCollection<IDevice> DiscoveredDevices { get; set; }
        private readonly Guid _serviceGuid = Guid.Parse("492a8e3d-2589-40b1-b9c2-419a7ce80f3c");
        private readonly Guid _previewGuid = Guid.Parse("492a8e3e-2589-40b1-b9c2-419a7ce80f3c");
        private readonly Guid _triggerGuid = Guid.Parse("492a8e3f-2589-40b1-b9c2-419a7ce80f3c");
        private readonly Guid _capturedGuid = Guid.Parse("492a8e40-2589-40b1-b9c2-419a7ce80f3c");
        private readonly Guid _helloGuid = Guid.Parse("492a8e41-2589-40b1-b9c2-419a7ce80f3c");

        public Command DisconnectCommand { get; set; }
        public Command InitialSetupCommand { get; set; }
        public Command PairedSetupCommand { get; set; }
        public Command AttemptConnectionCommand { get; set; }

        private int _initializeThreadLocker;
        private bool _isInitialized;
        private int _pairInitializeThreadLocker;
        private bool _isPairInitialized;
        private int _connectThreadLocker;
        private bool _isConnectionCallbackReady;

        public PairingViewModel()
        {
            var bluetooth = DependencyService.Get<IBluetooth>();
            DiscoveredDevices = new ObservableCollection<IDevice>();

            CrossBleAdapter.Current.WhenStatusChanged().Subscribe(status =>
            {
                Debug.WriteLine("### Status changed: " + status);
            });

            CrossBleAdapter.Current.WhenDeviceStateRestored().Subscribe(async device =>
            {
                Debug.WriteLine("### State restored: " + device.Status);
                if (device.IsConnected())
                {
                    await ShowConnectionSucceeded();
                } else if (device.IsDisconnected())
                {
                    await ShowDisconnected();
                }
            });

            DisconnectCommand = new Command(() =>
            {
                CrossBleAdapter.Current.GetConnectedDevices().Subscribe(async devices =>
                {
                    foreach (var device in devices)
                    {
                        device.CancelConnection();
                    }
                    await ShowDisconnected();
                });
            });

            InitialSetupCommand = new Command(async () =>
            {
                if (Interlocked.CompareExchange(ref _initializeThreadLocker, 1, 0) == 0)
                {
                    if (!_isInitialized)
                    {
                        if (!bluetooth.IsBluetoothSupported())
                        {
                            await CoreMethods.DisplayAlert("Bluetooth Not Supported",
                                "Bluetooth is not supported on this device.", "OK");
                            await CoreMethods.PopPageModel();
                            _initializeThreadLocker = 0;
                            return;
                        }

                        if (!await bluetooth.RequestBluetoothPermissions())
                        {
                            await DisplayPermissionsDeniedMessage();
                            _initializeThreadLocker = 0;
                            return;
                        }

                        if (!await bluetooth.TurnOnBluetooth())
                        {
                            await CoreMethods.DisplayAlert("Bluetooth Not On",
                                "This device failed to turn on bluetooth.", "OK");
                            _initializeThreadLocker = 0;
                            return;
                        }

                        if (!await bluetooth.RequestLocationPermissions())
                        {
                            await DisplayPermissionsDeniedMessage();
                            _initializeThreadLocker = 0;
                            return;
                        }

                        if (!await bluetooth.TurnOnLocationServices())
                        {
                            await DisplayPermissionsDeniedMessage();
                            _initializeThreadLocker = 0;
                            return;
                        }

                        if (CrossBleAdapter.Current.CanControlAdapterState())
                        {
                            CrossBleAdapter.Current.SetAdapterState(true);
                        }

                        if (bluetooth.IsServerSupported() &&
                            (Device.RuntimePlatform == Device.iOS ||
                             CrossBleAdapter.AndroidConfiguration.IsServerSupported))
                        {
                            var server = CrossBleAdapter.Current.CreateGattServer();
                            server.Subscribe(gattServer =>
                            {
                                gattServer.AddService(_serviceGuid, true, service =>
                                {
                                    Debug.WriteLine("### Service callback");
                                    service.AddCharacteristic(_previewGuid, CharacteristicProperties.Read,
                                        GattPermissions.Read);
                                    service.AddCharacteristic(_triggerGuid, CharacteristicProperties.Write,
                                        GattPermissions.Write);
                                    service.AddCharacteristic(_capturedGuid, CharacteristicProperties.Read,
                                        GattPermissions.Read);
                                    var helloCharacteristic = service.AddCharacteristic(_helloGuid,
                                        CharacteristicProperties.Write, GattPermissions.Write);
                                    helloCharacteristic.WhenWriteReceived().Subscribe(async request =>
                                    {
                                        var write = Encoding.UTF8.GetString(request.Value, 0, request.Value.Length);
                                        Debug.WriteLine("### Hello received: " + write);
                                        await ShowConnectionSucceeded();
                                    });
                                });
                                gattServer.WhenAnyCharacteristicSubscriptionChanged().Subscribe(subscription =>
                                {
                                    Debug.WriteLine("### Subscription changed: " +
                                                    subscription.Characteristic.Uuid);
                                });
                            });

                            CrossBleAdapter.Current.Advertiser.Stop();
                            CrossBleAdapter.Current.Advertiser.Start(new AdvertisementData
                            {
                                AndroidIsConnectable = true,
                                //TODO: something about android naming here - use device or specify or something, coming up blank
                                ServiceUuids = new List<Guid>
                                {
                                    _serviceGuid
                                }
                            });

                        }

                        DiscoveredDevices.Clear();
                        CrossBleAdapter.Current.StopScan();
                        CrossBleAdapter.Current.Scan(new ScanConfig
                        {
                            ServiceUuids = new List<Guid>
                            {
                                _serviceGuid
                            }
                        }).Subscribe(scanResult =>
                        {
                            Debug.WriteLine(
                                "### Device found: " + scanResult.Device.Name + " ID: " + scanResult.Device.Uuid);
                            if (DiscoveredDevices.All(d => d.Uuid.ToString() != scanResult.Device.Uuid.ToString()))
                            {
                                DiscoveredDevices.Add(scanResult.Device);
                            }
                        }, async exception =>
                        {
                            await Device.InvokeOnMainThreadAsync(async () =>
                            {
                                await CoreMethods.DisplayAlert("Device Failed to Search",
                                    "This device failed to search for discoverable devices over bluetooth.", "OK");
                            });
                        });

                        _isInitialized = true;
                    }

                    Debug.WriteLine("### Bluetooth initialized");

                    _initializeThreadLocker = 0;
                }
            });

            PairedSetupCommand = new Command(async obj =>
            {
                if (Interlocked.CompareExchange(ref _pairInitializeThreadLocker, 1, 0) == 0)
                {
                    if (!_isPairInitialized)
                    {
                        if (!bluetooth.IsBluetoothSupported())
                        {
                            await CoreMethods.DisplayAlert("Bluetooth Not Supported",
                                "Bluetooth is not supported on this device.", "OK");
                            await CoreMethods.PopPageModel();
                            _pairInitializeThreadLocker = 0;
                            return;
                        }

                        if (!await bluetooth.RequestBluetoothPermissions())
                        {
                            await DisplayPermissionsDeniedMessage();
                            _pairInitializeThreadLocker = 0;
                            return;
                        }

                        if (!await bluetooth.TurnOnBluetooth())
                        {
                            await CoreMethods.DisplayAlert("Bluetooth Not On",
                                "This device failed to turn on bluetooth.", "OK");
                            _pairInitializeThreadLocker = 0;
                            return;
                        }

                        if (CrossBleAdapter.Current.CanControlAdapterState())
                        {
                            CrossBleAdapter.Current.SetAdapterState(true);
                        }

                        if (bluetooth.IsServerSupported() &&
                            (Device.RuntimePlatform == Device.iOS ||
                             CrossBleAdapter.AndroidConfiguration.IsServerSupported))
                        {
                            var server = CrossBleAdapter.Current.CreateGattServer();
                            server.Subscribe(gattServer =>
                            {
                                gattServer.AddService(_serviceGuid, true, service =>
                                {
                                    Debug.WriteLine("### Service callback");
                                    service.AddCharacteristic(_previewGuid, CharacteristicProperties.Read,
                                        GattPermissions.Read);
                                    service.AddCharacteristic(_triggerGuid, CharacteristicProperties.Write,
                                        GattPermissions.Write);
                                    service.AddCharacteristic(_capturedGuid, CharacteristicProperties.Read,
                                        GattPermissions.Read);
                                    var helloCharacteristic = service.AddCharacteristic(_helloGuid,
                                        CharacteristicProperties.Write, GattPermissions.Write);
                                    helloCharacteristic.WhenWriteReceived().Subscribe(async request =>
                                    {
                                        var write = Encoding.UTF8.GetString(request.Value, 0, request.Value.Length);
                                        Debug.WriteLine("### Hello received: " + write);
                                        await ShowConnectionSucceeded();
                                    });
                                });
                                gattServer.WhenAnyCharacteristicSubscriptionChanged().Subscribe(subscription =>
                                {
                                    Debug.WriteLine("### Subscription changed: " +
                                                    subscription.Characteristic.Uuid);
                                });
                            });

                            CrossBleAdapter.Current.Advertiser.Stop();
                            CrossBleAdapter.Current.Advertiser.Start(new AdvertisementData
                            {
                                AndroidIsConnectable = true,
                                //TODO: something about android naming here - use device or specify or something, coming up blank
                                ServiceUuids = new List<Guid>
                                {
                                    _serviceGuid
                                }
                            });
                        }

                        _isPairInitialized = true;
                    }

                    Debug.WriteLine("### Bluetooth pair initialized");

                    _pairInitializeThreadLocker = 0;
                }
            });

            AttemptConnectionCommand = new Command(async obj =>
            {
                try
                {
                    if (Interlocked.CompareExchange(ref _connectThreadLocker, 1, 0) == 0)
                    {
                        var device = (IDevice)obj;
                        if (!_isConnectionCallbackReady)
                        {
                            device.WhenStatusChanged().Subscribe(async status =>
                            {
                                Debug.WriteLine("### Connected device status changed: " + status);
                                if (status == ConnectionStatus.Connected &&
                                    !IsConnected)
                                {
                                    await ShowConnectionSucceeded();
                                    device.DiscoverServices().Subscribe(service =>
                                    {
                                        Debug.WriteLine("### Service discovered: " + service.Description + ", " + service.Uuid);
                                        service.DiscoverCharacteristics().Subscribe(characteristic =>
                                        {
                                            Debug.WriteLine("### Characteristic discovered: " + characteristic.Description + ", " + characteristic.Uuid);
                                            if (characteristic.Uuid == _helloGuid)
                                            {
                                                characteristic.Write(Encoding.UTF8.GetBytes("Hi there friend.")).Subscribe(resp =>
                                                {
                                                    Debug.WriteLine("### Hello write response came back.");
                                                });
                                            }
                                        });
                                    });
                                } 
                                else if (status == ConnectionStatus.Disconnected && 
                                         IsConnected)
                                {
                                    await ShowDisconnected();
                                }
                            }, async exception =>
                            {
                                await Device.InvokeOnMainThreadAsync(async () =>
                                {
                                    await CoreMethods.DisplayAlert("Device Failed to Connect",
                                        "This device failed to connect over bluetooth. Please try again. Error: " +
                                        exception.Message, "OK");
                                });
                            });

                            _isConnectionCallbackReady = true;
                        }

                        device.PairingRequest().Subscribe(didPair =>
                        {
                            Debug.WriteLine("### Pairing requested: " + didPair);
                            if (didPair)
                            {
                                GetPairedDevices();
                                device.Connect();
                            }
                        });

                        _connectThreadLocker = 0;
                    }
                }
                catch (Exception e)
                {
                    await CoreMethods.DisplayAlert("Device Failed to Connect",
                        "This device failed to connect over bluetooth. Please try again. Error: " + e.Message, "OK");
                }
            });
        }

        private void GetPairedDevices()
        {
            CrossBleAdapter.Current.GetPairedDevices().Subscribe(devices =>
            {
                PairedDevices = new ObservableCollection<IDevice>(devices);
            }, async exception =>
            {
                await Device.InvokeOnMainThreadAsync(async () =>
                {
                    await CoreMethods.DisplayAlert("Failed to Get Paired Devices",
                        "This device failed to get paired devices.", "OK");
                });
            });
        }

        private async Task ShowConnectionSucceeded()
        {
            CrossBleAdapter.Current.Advertiser.Stop();
            CrossBleAdapter.Current.StopScan();
            IsConnected = true;
            GetPairedDevices();
            await Device.InvokeOnMainThreadAsync(async () =>
            {
                await CoreMethods.DisplayAlert("Connection Success", "Congrats!", "Yay");
            });
        }

        private async Task ShowDisconnected()
        {
            IsConnected = false;
            await Device.InvokeOnMainThreadAsync(async () =>
            {
                await CoreMethods.DisplayAlert("Disconnected", "Disconnected!", "OK");
            });
        }

        private async Task DisplayPermissionsDeniedMessage()
        {
            await CoreMethods.DisplayAlert("Permissions Denied",
                "Permissions required for pairing were denied.", "OK");
        }

        protected override void ViewIsAppearing(object sender, EventArgs e)
        {
            base.ViewIsAppearing(sender, e);
            GetPairedDevices();
        }

        protected override void ViewIsDisappearing(object sender, EventArgs e)
        {
            CrossBleAdapter.Current.StopScan();
            CrossBleAdapter.Current.Advertiser.Stop();
            base.ViewIsDisappearing(sender, e);
        }
    }
}