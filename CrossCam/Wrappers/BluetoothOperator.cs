using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Reactive.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using FreshMvvm;
using Plugin.BluetoothLE;
using Plugin.BluetoothLE.Server;
using Xamarin.Forms;
using IDevice = Plugin.BluetoothLE.IDevice;

namespace CrossCam.Wrappers
{
    public sealed class BluetoothOperator : INotifyPropertyChanged
    {
        public bool IsConnected => ConnectionStatus == ConnectionStatus.Connected;
        private ConnectionStatus ConnectionStatus { get; set; }
        public bool IsPrimary { get; private set; }
        public IPageModelCoreMethods CurrentCoreMethods { get; set; }
        private readonly IPlatformBluetooth _bleSetup;
        private readonly Guid _serviceGuid = Guid.Parse("492a8e3d-2589-40b1-b9c2-419a7ce80f3c");
        private readonly Guid _timeGuid = Guid.Parse("492a8e3e-2589-40b1-b9c2-419a7ce80f3c");
        private readonly Guid _previewGuid = Guid.Parse("492a8e3f-2589-40b1-b9c2-419a7ce80f3c");
        private readonly Guid _triggerGuid = Guid.Parse("492a8e40-2589-40b1-b9c2-419a7ce80f3c");
        private readonly Guid _capturedGuid = Guid.Parse("492a8e41-2589-40b1-b9c2-419a7ce80f3c");
        private readonly Guid _helloGuid = Guid.Parse("492a8e42-2589-40b1-b9c2-419a7ce80f3c");
        private IDevice _device;
        private readonly Timer _captureSyncTimer = new Timer{AutoReset = false};

        private const long CAPTURE_BUFFER_MS = 1000;

        public event ElapsedEventHandler CaptureRequested;
        private void OnCaptureRequested(object sender, ElapsedEventArgs e)
        {
            Debug.WriteLine("CAPTURE NOW!!!!");
            _captureSyncTimer.Elapsed -= OnCaptureRequested;
            var handler = CaptureRequested;
            handler?.Invoke(this, e);
        }

        public event EventHandler Disconnected;
        private void OnDisconnected(EventArgs e)
        {
            ShowPairDisconnected();
            var handler = Disconnected;
            handler?.Invoke(this, e);
        }

        public event EventHandler Connected;
        private void OnConnected(EventArgs e)
        {
            ShowPairConnected();
            var handler = Connected;
            handler?.Invoke(this, e);
        }

        public event EventHandler<ErrorEventArgs> ErrorOccurred;
        private void OnErrorOccurred(ErrorEventArgs e)
        {
            ShowPairErrorOccurred(e.Step, e.Exception.ToString());
            var handler = ErrorOccurred;
            handler?.Invoke(this, e);
        }

        public event EventHandler<BluetoothDeviceDiscoveredEventArgs> DeviceDiscovered;
        private void OnDeviceDiscovered(BluetoothDeviceDiscoveredEventArgs e)
        {
            var handler = DeviceDiscovered;
            handler?.Invoke(this, e);
        }

        public event EventHandler<PairedDevicesFoundEventArgs> PairedDevicesFound;
        private void OnPairedDevicesFound(PairedDevicesFoundEventArgs e)
        {
            var handler = PairedDevicesFound;
            handler?.Invoke(this, e);
        }

        public event EventHandler<PreviewFrameReadyEventArgs> PreviewFrameReady;
        private void OnPreviewFrameReady(PreviewFrameReadyEventArgs e)
        {
            var handler = PreviewFrameReady;
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
                ConnectionStatus = device.Status;
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
                        service.AddCharacteristic(_timeGuid, CharacteristicProperties.Read,
                        GattPermissions.Read).WhenReadReceived().Subscribe(request =>
                        {
                            request.Value = Encoding.UTF8.GetBytes(DateTime.UtcNow.Ticks.ToString());
                        }, exception =>
                        {
                            OnErrorOccurred(new ErrorEventArgs
                            {
                                Exception = exception,
                                Step = "Provide Time for Sync"
                            });
                        });
                        service.AddCharacteristic(_triggerGuid, CharacteristicProperties.Write,
                            GattPermissions.Write).WhenWriteReceived().Subscribe(request =>
                        {
                            Task.Run(() =>
                            {
                                HandleIncomingSyncRequest(request.Value);
                            });
                        }, exception =>
                        {
                            OnErrorOccurred(new ErrorEventArgs
                            {
                                Step = "Receiving Trigger",
                                Exception = exception
                            });
                        });
                        service.AddCharacteristic(_capturedGuid, CharacteristicProperties.Read,
                            GattPermissions.Read);
                        service.AddCharacteristic(_helloGuid,
                            CharacteristicProperties.Write, GattPermissions.Write).WhenWriteReceived().Subscribe(request =>
                        {
                            IsPrimary = false;
                            var write = Encoding.UTF8.GetString(request.Value, 0, request.Value.Length);
                            Debug.WriteLine("### Hello received: " + write);
                            ConnectionStatus = ConnectionStatus.Connected;
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
            device.WhenStatusChanged().Subscribe(newStatus =>
            {
                Debug.WriteLine("### Connected device status changed: " + newStatus);
                if (newStatus == ConnectionStatus.Connected &&
                    !IsConnected)
                {
                    _device = device;
                    OnConnected(null);

                    device.DiscoverServices().Subscribe(service =>
                    {
                        Debug.WriteLine("### Service discovered: " + service.Description + ", " + service.Uuid);
                        service.DiscoverCharacteristics().Subscribe(characteristic =>
                        {
                            Debug.WriteLine("### Characteristic discovered: " + characteristic.Description + ", " + characteristic.Uuid);
                            if (characteristic.Uuid == _helloGuid)
                            {
                                IsPrimary = true;
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
                else if (newStatus == ConnectionStatus.Disconnected &&
                         IsConnected ||
                         newStatus == ConnectionStatus.Disconnected &&
                         ConnectionStatus == ConnectionStatus.Connecting)
                {
                    OnDisconnected(null);
                }

                ConnectionStatus = newStatus;
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

                ConnectionStatus = ConnectionStatus.Disconnected;
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

        public async void RequestSyncForCaptureAndSync()
        {
            var timeOffset = 0L;
            var roundTripDelay = 0L;
            const int NUMBER_OF_RUNS = 5;
            try
            {
                for (var i = 0; i < NUMBER_OF_RUNS; i++)
                {
                    var t0 = DateTime.UtcNow.Ticks;
                    var readResult = await _device.ReadCharacteristic(_serviceGuid, _timeGuid).ToTask();
                    var t3 = DateTime.UtcNow.Ticks;
                    var t1t2String = Encoding.UTF8.GetString(readResult.Data, 0, readResult.Data.Length);
                    if (long.TryParse(t1t2String, out var t1t2))
                    {
                        var timeOffsetRun = ((t1t2 - t0) + (t1t2 - t3)) / 2;
                        timeOffset += timeOffsetRun;
                        var roundTripDelayRun = t3 - t0;
                        roundTripDelay += roundTripDelayRun; 
                        Debug.WriteLine("Time offset: " + timeOffsetRun / 10000d + " milliseconds");
                        Debug.WriteLine("Round trip delay: " + roundTripDelayRun / 10000d + " milliseconds");
                    }
                }

                timeOffset /= NUMBER_OF_RUNS;
                roundTripDelay /= NUMBER_OF_RUNS;
                Debug.WriteLine("Average time offset: " + timeOffset / 10000d + " milliseconds");
                Debug.WriteLine("Average round trip delay: " + roundTripDelay / 10000d + " milliseconds");
            }
            catch (Exception e)
            {
                OnErrorOccurred(new ErrorEventArgs
                {
                    Exception = e,
                    Step = "Send Read Time Request"
                });
            }

            var targetSyncMoment = DateTime.UtcNow.AddMilliseconds(CAPTURE_BUFFER_MS);
            var partnerSyncMoment = targetSyncMoment.AddTicks(timeOffset).AddTicks(-roundTripDelay / 2);
            _device.WriteCharacteristic(_serviceGuid, _triggerGuid, Encoding.UTF8.GetBytes(partnerSyncMoment.Ticks.ToString())).Subscribe(what =>
            {
                SyncCapture(targetSyncMoment);
            }, exception =>
            {
                OnErrorOccurred(new ErrorEventArgs
                {
                    Exception = exception,
                    Step = "Sending Trigger"
                });
            });
        }

        private void HandleIncomingSyncRequest(byte[] masterTicksByteArray)
        {
            try
            {
                var targetTimeString = Encoding.UTF8.GetString(masterTicksByteArray, 0, masterTicksByteArray.Length);
                if (long.TryParse(targetTimeString, out var targetTimeTicks))
                {
                    var targetTime = new DateTime(targetTimeTicks);
                    SyncCapture(targetTime);
                    Debug.WriteLine("Target time: " + targetTime.ToString("O"));
                }
            }
            catch (Exception e)
            {
                OnErrorOccurred(new ErrorEventArgs
                {
                    Exception = e,
                    Step = "Receive Sync for Capture"
                });
            }
        }

        private void SyncCapture(DateTime syncTime)
        {
            _captureSyncTimer.Elapsed += OnCaptureRequested;
            var interval = (syncTime.Ticks - DateTime.UtcNow.Ticks) / 10000d;
            _captureSyncTimer.Interval = interval;
            _captureSyncTimer.Start();
            Debug.WriteLine("Sync interval set: " + interval);
            Debug.WriteLine("Sync time: " + syncTime.ToString("O"));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private async void ShowPairErrorOccurred(string step, string details)
        {
            await Device.InvokeOnMainThreadAsync(async () =>
            {
                await CurrentCoreMethods.DisplayAlert("Pair Error Occurred",
                    "An error occurred during " + step + ", exception: " + details, "OK");
            });
        }

        private async void ShowPairDisconnected()
        {
            await Device.InvokeOnMainThreadAsync(async () =>
            {
                await CurrentCoreMethods.DisplayAlert("Disconnected", "The connection to the paired device was lost. Please connect again.",
                    "OK");
            });
        }

        private async void ShowPairConnected()
        {
            await Device.InvokeOnMainThreadAsync(async () =>
            {
                await CurrentCoreMethods.DisplayAlert("Connected Pair Device", "Pair device connected successfully!", "Yay");
            });
        }
    }

    public class PreviewFrameReadyEventArgs : EventArgs
    {
        public byte[] Frame { get; set; }
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