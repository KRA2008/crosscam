using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossCam.Wrappers;
using FreshMvvm;
using Xamarin.Forms;

namespace CrossCam.ViewModel
{
    public class PairingViewModel : FreshBasePageModel
    {
        private readonly IBluetooth _bluetooth;
        private int _isInitialized;
        public bool IsConnected =>
            _bluetooth != null && 
            _bluetooth.IsConnected();
        public ObservableCollection<PartnerDevice> PairedDevices { get; set; }
        public ObservableCollection<PartnerDevice> DiscoveredDevices { get; set; }

        public Command DisconnectCommand { get; set; }
        public Command InitializeBluetoothCommand { get; set; }
        public Command BecomeDiscoverableCommand { get; set; }
        public Command SearchForDevicesCommand { get; set; }
        public Command AttemptConnectionCommand { get; set; }
        public Command ForgetDeviceCommand { get; set; }

        public PairingViewModel()
        {
            _bluetooth = DependencyService.Get<IBluetooth>();
            DiscoveredDevices = new ObservableCollection<PartnerDevice>();

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
                        PairedDevices = new ObservableCollection<PartnerDevice>(_bluetooth.GetPairedDevices());
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
                                try
                                {
                                    var didConnect = await _bluetooth.ListenForConnections();
                                    if (didConnect.HasValue)
                                    {
                                        if (!didConnect.Value)
                                        {
                                            await CoreMethods.DisplayAlert("Bluetooth Listening Timed Out",
                                                "This device timed out waiting for connections over bluetooth. Please navigate away from and back to this page.",
                                                "OK");
                                        }
                                        else
                                        {
                                            await ConnectionSucceeded();
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    await CoreMethods.DisplayAlert("Bluetooth Listening Failed",
                                        "This device failed to begin listening for connections over bluetooth. Please navigate away from and back to this page. Error: " + e.Message,
                                        "OK");
                                }
                            }
                        }
                        RaisePropertyChanged(nameof(IsConnected));
                    }
                }
            });

            BecomeDiscoverableCommand = new Command(async () =>
            {
                if (!await _bluetooth.BecomeDiscoverable())
                {
                    await CoreMethods.DisplayAlert("Device Failed to Become Discoverable",
                        "This device failed to become discoverable over bluetooth.", "OK");
                }
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
                        if (!_bluetooth.BeginSearchForDiscoverableDevices())
                        {
                            await CoreMethods.DisplayAlert("Device Failed to Search",
                                "This device failed to search for discoverable devices over bluetooth.", "OK");
                        }
                    }
                }
            });

            AttemptConnectionCommand = new Command(async obj =>
            {
                try
                {
                    if (!await _bluetooth.AttemptConnection((PartnerDevice) obj))
                    {
                        RaisePropertyChanged(nameof(IsConnected));
                        await CoreMethods.DisplayAlert("Device Failed to Connect",
                            "This device failed to connect over bluetooth. Please try again.", "OK");
                    }
                    else
                    {
                        await ConnectionSucceeded();
                    }
                }
                catch (Exception e)
                {
                    await CoreMethods.DisplayAlert("Device Failed to Connect",
                        "This device failed to connect over bluetooth. Please try again. Error: " + e.Message, "OK");
                }
            });

            ForgetDeviceCommand = new Command(async obj =>
            {
                _bluetooth.ForgetDevice((PartnerDevice)obj);
                await Task.Delay(500);
                PairedDevices = new ObservableCollection<PartnerDevice>(_bluetooth.GetPairedDevices());
            });
        }

        private void BluetoothOnDeviceDiscovered(object sender, PartnerDevice e)
        {
            if (DiscoveredDevices.All(d => d.Address != e.Address))
            {
                DiscoveredDevices.Add(e);
            }
        }

        private async Task ConnectionSucceeded()
        {
            RaisePropertyChanged(nameof(IsConnected));
            await CoreMethods.DisplayAlert("Connection Success", "Congrats!", "Yay");
            PairedDevices = new ObservableCollection<PartnerDevice>(_bluetooth.GetPairedDevices());
        }

        private async Task DisplayPermissionsDeniedMessage()
        {
            await CoreMethods.DisplayAlert("Permissions Denied",
                "Permissions required for pairing were denied.", "OK");
        }

        protected override void ViewIsAppearing(object sender, EventArgs e)
        {
            base.ViewIsAppearing(sender, e);
            _bluetooth.DeviceDiscovered += BluetoothOnDeviceDiscovered;
            InitializeBluetoothCommand.Execute(null);
        }

        protected override void ViewIsDisappearing(object sender, EventArgs e)
        {
            _bluetooth.DeviceDiscovered -= BluetoothOnDeviceDiscovered;
            base.ViewIsDisappearing(sender, e);
        }
    }
}