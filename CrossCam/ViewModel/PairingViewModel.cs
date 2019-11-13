﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
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
        private readonly IBluetooth _bluetooth;
        private int _isInitialized;
        public bool IsConnected { get; set; }
        public ObservableCollection<IDevice> PairedDevices { get; set; }
        public ObservableCollection<IDevice> DiscoveredDevices { get; set; }
        public const string SDP_UUID = "492a8e3d-2589-40b1-b9c2-419a7ce80f3c";

        public Command DisconnectCommand { get; set; }
        public Command InitializeBluetoothCommand { get; set; }
        public Command BecomeDiscoverableCommand { get; set; }
        public Command SearchForDevicesCommand { get; set; }
        public Command AttemptConnectionCommand { get; set; }
        public Command ForgetDeviceCommand { get; set; }

        public PairingViewModel()
        {
            _bluetooth = DependencyService.Get<IBluetooth>();
            DiscoveredDevices = new ObservableCollection<IDevice>();

            CrossBleAdapter.Current.WhenStatusChanged().Subscribe(status =>
            {
                Debug.WriteLine("### Status changed: " + status);
            });

            DisconnectCommand = new Command(() =>
            {
                _bluetooth.Disconnect();
                RaisePropertyChanged(nameof(IsConnected));
            });

            InitializeBluetoothCommand = new Command(async () =>
            {
                if (Interlocked.CompareExchange(ref _isInitialized, 1, 0) == 0)
                {
                    if (!_bluetooth.IsBluetoothSupported())
                    {
                        await CoreMethods.DisplayAlert("Bluetooth Not Supported",
                            "Bluetooth is not supported on this device.", "OK");
                        await CoreMethods.PopPageModel();
                    }
                    else
                    {
                        GetPairedDevices();
                        if (!await _bluetooth.RequestBluetoothPermissions())
                        {
                            await DisplayPermissionsDeniedMessage();
                        }
                        else
                        {
                            if (!await _bluetooth.TurnOnBluetooth())
                            {
                                await CoreMethods.DisplayAlert("Bluetooth Not On",
                                    "This device failed to turn on bluetooth.", "OK");
                            }
                            else
                            {
                                if (CrossBleAdapter.Current.CanControlAdapterState())
                                {
                                    CrossBleAdapter.Current.SetAdapterState(true);
                                }
                            }
                        }
                        RaisePropertyChanged(nameof(IsConnected));
                    }
                }
            });

            BecomeDiscoverableCommand = new Command(() =>
            {
                var server = CrossBleAdapter.Current.CreateGattServer();
                server.Subscribe(gattServer =>
                {
                    gattServer.AddService(Guid.Parse(SDP_UUID), true, service =>
                    {
                        Debug.WriteLine("### Service callback");
                        service.AddCharacteristic(
                            Guid.Parse(SDP_UUID),
                            CharacteristicProperties.Read | CharacteristicProperties.Write | CharacteristicProperties.WriteNoResponse,
                            GattPermissions.Read | GattPermissions.Write
                        );
                    });
                });

                CrossBleAdapter.Current.Advertiser.Stop();
                CrossBleAdapter.Current.Advertiser.Start(new AdvertisementData
                {
                    //TODO: something about android naming here - use device or specify or something, comes up blank on Nexus 4
                    ServiceUuids = new List<Guid>
                    {
                        Guid.Parse(SDP_UUID)
                    }
                });
            });

            SearchForDevicesCommand = new Command(async () =>
            {
                if (!await _bluetooth.RequestLocationPermissions())
                {
                    await DisplayPermissionsDeniedMessage();
                }
                else
                {
                    if (!await _bluetooth.TurnOnLocationServices())
                    {
                        await CoreMethods.DisplayAlert("Location Services Off",
                            "Location services were not turned on.", "OK");
                    }
                    else
                    {
                        DiscoveredDevices.Clear();
                        CrossBleAdapter.Current.StopScan();
                        CrossBleAdapter.Current.Scan(new ScanConfig
                        {
                            ServiceUuids = new List<Guid>
                            {
                                Guid.Parse(SDP_UUID)
                            }
                        }).Subscribe(scanResult =>
                        {
                            Debug.WriteLine("### Device found: " + scanResult.Device.Name + " ID: " + scanResult.Device.Uuid);
                            if (DiscoveredDevices.All(d => d.Uuid.ToString() != scanResult.Device.Uuid.ToString()))
                            {
                                DiscoveredDevices.Add(scanResult.Device);
                            }
                        }, async exception =>
                        {      
                            await CoreMethods.DisplayAlert("Device Failed to Search", 
                                "This device failed to search for discoverable devices over bluetooth.", "OK");
                        });
                    }
                }
            });

            AttemptConnectionCommand = new Command(async obj =>
            {
                try
                {
                    var device = (IDevice) obj;
                    device.WhenStatusChanged().Subscribe(async status =>
                    {
                        Debug.WriteLine("### Connected device status changed: " + status);
                        await ConnectionSucceeded();
                    }, async exception =>
                    {
                        await CoreMethods.DisplayAlert("Device Failed to Connect",
                            "This device failed to connect over bluetooth. Please try again. Error: " + exception.Message, "OK");
                    });
                    device.Connect();
                }
                catch (Exception e)
                {
                    await CoreMethods.DisplayAlert("Device Failed to Connect",
                        "This device failed to connect over bluetooth. Please try again. Error: " + e.Message, "OK");
                }
            });

            ForgetDeviceCommand = new Command(async obj =>
            {
                var device = (IDevice) obj;
                //TODO: how to forget?
                await Task.Delay(500); 
                GetPairedDevices();
            });
        }

        private void GetPairedDevices()
        {
            CrossBleAdapter.Current.GetPairedDevices().Subscribe(devices =>
            {
                PairedDevices = new ObservableCollection<IDevice>(devices);
            }, async exception =>
            {
                await CoreMethods.DisplayAlert("Failed to Get Paired Devices",
                    "This device failed to get paired devices.", "OK");
            });
        }

        private async Task ConnectionSucceeded()
        {
            RaisePropertyChanged(nameof(IsConnected));
            await CoreMethods.DisplayAlert("Connection Success", "Congrats!", "Yay"); 
            CrossBleAdapter.Current.GetPairedDevices().Subscribe(devices =>
            {
                PairedDevices = new ObservableCollection<IDevice>(devices);
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
            InitializeBluetoothCommand.Execute(null);
        }

        protected override void ViewIsDisappearing(object sender, EventArgs e)
        {
            CrossBleAdapter.Current.StopScan();
            CrossBleAdapter.Current.Advertiser.Stop();
            base.ViewIsDisappearing(sender, e);
        }
    }
}