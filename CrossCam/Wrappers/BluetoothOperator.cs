using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Plugin.BluetoothLE;
using Plugin.BluetoothLE.Server;
using Xamarin.Forms;
using IDevice = Plugin.BluetoothLE.IDevice;

namespace CrossCam.Wrappers
{
    public class BluetoothOperator
    {
        private bool _isConnected;
        private readonly IPlatformBluetooth _bleSetup;
        private readonly Guid _serviceGuid = Guid.Parse("492a8e3d-2589-40b1-b9c2-419a7ce80f3c");
        private readonly Guid _previewGuid = Guid.Parse("492a8e3e-2589-40b1-b9c2-419a7ce80f3c");
        private readonly Guid _triggerGuid = Guid.Parse("492a8e3f-2589-40b1-b9c2-419a7ce80f3c");
        private readonly Guid _capturedGuid = Guid.Parse("492a8e40-2589-40b1-b9c2-419a7ce80f3c");
        private readonly Guid _helloGuid = Guid.Parse("492a8e41-2589-40b1-b9c2-419a7ce80f3c");

        public event EventHandler Disconnected;
        protected virtual void OnDisconnected(EventArgs e)
        {
            var handler = Disconnected;
            handler?.Invoke(this, e);
        }

        public event EventHandler Connected;
        protected virtual void OnConnected(EventArgs e)
        {
            var handler = Connected;
            handler?.Invoke(this, e);
        }

        public event EventHandler<ErrorEventArgs> ErrorOccurred;
        protected virtual void OnErrorOccurred(ErrorEventArgs e)
        {
            var handler = ErrorOccurred;
            handler?.Invoke(this, e);
        }

        public event EventHandler<BluetoothDeviceDiscoveredEventArgs> DeviceDiscovered;
        protected virtual void OnDeviceDiscovered(BluetoothDeviceDiscoveredEventArgs e)
        {
            var handler = DeviceDiscovered;
            handler?.Invoke(this, e);
        }

        public event EventHandler<PairedDevicesFoundEventArgs> PairedDevicesFound;
        protected virtual void OnPairedDevicesFound(PairedDevicesFoundEventArgs e)
        {
            var handler = PairedDevicesFound;
            handler?.Invoke(this, e);
        }


        public BluetoothOperator()
        {
            _bleSetup = DependencyService.Get<IPlatformBluetooth>();

            CrossBleAdapter.Current.WhenStatusChanged().Subscribe(status =>
            {
                Debug.WriteLine("### Status changed: " + status);
            }, exception =>
            {
                OnErrorOccurred(new ErrorEventArgs
                {
                    Step = "Bluetooth Status Changed",
                    Exception = exception
                });
            });

            CrossBleAdapter.Current.WhenDeviceStateRestored().Subscribe(device =>
            {
                Debug.WriteLine("### State restored: " + device.Status);
                if (device.IsConnected())
                {
                    OnConnected(null);
                }
                else if (device.IsDisconnected())
                {
                    OnDisconnected(null);
                }
            }, exception =>
            {
                OnErrorOccurred(new ErrorEventArgs
                {
                    Step = "Device State Restored",
                    Exception = exception
                });
            });
        }

        public async Task InitializeForPairing()
        {
            if (!await _bleSetup.RequestLocationPermissions())
            {
                throw new PermissionsException();
            }

            if (!await _bleSetup.TurnOnLocationServices())
            {
                throw new PermissionsException();
            }

            await CreateGattServerAndStartAdvertisingIfCapable();

            CrossBleAdapter.Current.StopScan();
            CrossBleAdapter.Current.Scan(new ScanConfig
            {
                ServiceUuids = new List<Guid>
                    {
                        _serviceGuid
                    }
            }).Subscribe(scanResult =>
            {
                Debug.WriteLine("### Device found: " + scanResult.Device.Name + " ID: " + scanResult.Device.Uuid);
                OnDeviceDiscovered(new BluetoothDeviceDiscoveredEventArgs
                {
                    Device = scanResult.Device
                });
            }, exception => OnErrorOccurred(new ErrorEventArgs
            {
                Step = "Scan For Devices",
                Exception = exception
            }));

            Debug.WriteLine("### Bluetooth initialized");
        }

        public async Task InitializeForPairedConnection()
        {
            await CreateGattServerAndStartAdvertisingIfCapable();
        }

        private async Task CreateGattServerAndStartAdvertisingIfCapable()
        {
            if (!_bleSetup.IsBluetoothSupported())
            {
                throw new BluetoothNotSupportedException();
            }

            if (!await _bleSetup.RequestBluetoothPermissions())
            {
                throw new PermissionsException();
            }

            if (!await _bleSetup.TurnOnBluetooth())
            {
                throw new BluetoothNotTurnedOnException();
            }

            if (CrossBleAdapter.Current.CanControlAdapterState())
            {
                CrossBleAdapter.Current.SetAdapterState(true);
            }

            if (_bleSetup.IsBluetoothApiLevelSufficient() &&
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
                        helloCharacteristic.WhenWriteReceived().Subscribe(request =>
                        {
                            var write = Encoding.UTF8.GetString(request.Value, 0, request.Value.Length);
                            Debug.WriteLine("### Hello received: " + write);
                            _isConnected = true;
                            OnConnected(null);
                        }, exception =>
                        {
                            OnErrorOccurred(new ErrorEventArgs
                            {
                                Step = "Hello",
                                Exception = exception
                            });
                        });
                    });
                }, exception =>
                {
                    OnErrorOccurred(new ErrorEventArgs
                    {
                        Step = "Server Setup",
                        Exception = exception
                    });
                });

                CrossBleAdapter.Current.Advertiser.Stop();
                CrossBleAdapter.Current.Advertiser.Start(new AdvertisementData
                {
                    AndroidIsConnectable = true,
                    ServiceUuids = new List<Guid>
                    {
                        _serviceGuid
                    }
                });
            }
        }

        public void Connect(IDevice device)
        {
            device.WhenStatusChanged().Subscribe(status =>
            {
                Debug.WriteLine("### Connected device status changed: " + status);
                if (status == ConnectionStatus.Connected &&
                    !_isConnected)
                {
                    _isConnected = true;
                    OnConnected(null);

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
                                }, exception =>
                                {
                                    OnErrorOccurred(new ErrorEventArgs
                                    {
                                        Step = "Writing Hello",
                                        Exception = exception
                                    });
                                });
                            }
                        });
                    });
                }
                else if (status == ConnectionStatus.Disconnected &&
                         _isConnected)
                {
                    _isConnected = false;
                    OnDisconnected(null);
                }
            }, exception =>
            {
                OnErrorOccurred(new ErrorEventArgs
                {
                    Step = "Connected Device Status",
                    Exception = exception
                });
            });

            if (Device.RuntimePlatform == Device.Android &&
                _bleSetup.IsBluetoothApiLevelSufficient() &&
                device.IsPairingAvailable())
            {
                device.PairingRequest().Subscribe(didPair =>
                {
                    Debug.WriteLine("### Pairing requested: " + didPair);
                    if (didPair)
                    {
                        GetPairedDevices();
                        device.Connect(new ConnectionConfig
                        {
                            AutoConnect = true
                        });
                    }
                }, exception =>
                {
                    OnErrorOccurred(new ErrorEventArgs
                    {
                        Step = "Pairing",
                        Exception = exception
                    });
                });
            }
            else
            {
                device.Connect(new ConnectionConfig
                {
                    AutoConnect = true
                });
            }
        }

        public void Disconnect()
        {
            CrossBleAdapter.Current.GetConnectedDevices().Subscribe(devices =>
            {
                foreach (var device in devices)
                {
                    device.CancelConnection();
                }

                OnDisconnected(null);
            }, exception =>
            {
                OnErrorOccurred(new ErrorEventArgs
                {
                    Step = "Disconnect",
                    Exception = exception
                });
            });
        }

        public void GetPairedDevices()
        {
            CrossBleAdapter.Current.GetPairedDevices().Subscribe(devices =>
            {
                OnPairedDevicesFound(new PairedDevicesFoundEventArgs
                {
                    Devices = devices
                });
            }, exception =>
            {
                OnErrorOccurred(new ErrorEventArgs
                {
                    Step = "Get Paired Devices",
                    Exception = exception
                });
            });
        }
    }

    public class PairedDevicesFoundEventArgs : EventArgs
    {
        public IEnumerable<IDevice> Devices { get; set; }
    }

    public class ErrorEventArgs : EventArgs
    {
        public string Step { get; set; }
        public Exception Exception { get; set; }
    }

    public class BluetoothDeviceDiscoveredEventArgs : EventArgs
    {
        public IDevice Device { get; set; }
    }

    public class PermissionsException : Exception {}
    public class BluetoothNotSupportedException : Exception {}
    public class BluetoothNotTurnedOnException : Exception {}
    public class BluetoothFailedToSearchException : Exception {}
}