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
        public ObservableCollection<PartnerDevice> PairedDevices { get; set; }
        public ObservableCollection<PartnerDevice> DiscoveredDevices { get; set; }

        public Command InitializeBluetoothCommand { get; set; }
        public Command BecomeDiscoverableCommand { get; set; }
        public Command SearchForDevicesCommand { get; set; }
        public Command AttemptConnectionCommand { get; set; }
        public Command ForgetDeviceCommand { get; set; }

        public PairingViewModel()
        {
            _bluetooth = DependencyService.Get<IBluetooth>();
            DiscoveredDevices = new ObservableCollection<PartnerDevice>();

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
                        if (!await _bluetooth.RequestBluetoothPermissions())
                        {
                            await CoreMethods.DisplayAlert("Permissions Denied",
                                "Permissions required for pairing were denied.", "OK");
                        }
                        else
                        {
                            PairedDevices = new ObservableCollection<PartnerDevice>(_bluetooth.GetPairedDevices());
                            if (!await _bluetooth.TurnOnBluetooth())
                            {
                                await CoreMethods.DisplayAlert("Bluetooth Not On",
                                    "This device failed to turn on bluetooth.", "OK");
                            }
                            else
                            {
                                if (!await _bluetooth.ListenForConnections())
                                {
                                    await CoreMethods.DisplayAlert("Bluetooth Listening Failed",
                                        "This device failed to begin listening for connections over bluetooth.", "OK");
                                }
                                else
                                {
                                    PairedDevices = new ObservableCollection<PartnerDevice>(_bluetooth.GetPairedDevices());
                                    await DisplayConnectionSuccessMessage();
                                }
                            }
                        }
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
                DiscoveredDevices.Clear();
                if (!_bluetooth.BeginSearchForDiscoverableDevices())
                {
                    await CoreMethods.DisplayAlert("Device Failed to Search",
                        "This device failed to search for discoverable devices over bluetooth.", "OK");
                }
            });

            AttemptConnectionCommand = new Command(async obj =>
            {
                if (!await _bluetooth.AttemptConnection((PartnerDevice)obj))
                {
                    await CoreMethods.DisplayAlert("Device Failed to Connect",
                        "This device failed to connect over bluetooth.", "OK");
                }
                else
                {
                    PairedDevices = new ObservableCollection<PartnerDevice>(_bluetooth.GetPairedDevices());
                    await DisplayConnectionSuccessMessage();
                }
            });

            ForgetDeviceCommand = new Command(obj =>
            {
                _bluetooth.ForgetDevice((PartnerDevice)obj);
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

        private async Task DisplayConnectionSuccessMessage()
        {
            await CoreMethods.DisplayAlert("Connection Success", "Congrats!", "Yay");
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