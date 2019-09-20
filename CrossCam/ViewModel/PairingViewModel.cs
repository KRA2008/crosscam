using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CrossCam.Wrappers;
using FreshMvvm;
using Xamarin.Forms;

namespace CrossCam.ViewModel
{
    public class PairingViewModel : FreshBasePageModel
    {
        public ObservableCollection<PartnerDevice> PartnerDevices { get; set; }
        public ObservableCollection<PartnerDevice> AvailableDevices { get; set; }

        public Command InitializeBluetoothCommand { get; set; }

        public Command BecomeDiscoverableSecondaryCommand { get; set; }
        public Command SearchForDevicesPrimaryCommand { get; set; }
        public Command AttemptConnectionPrimaryCommand { get; set; }

        public Command ForgetDeviceCommand { get; set; }

        public PairingViewModel()
        {
            var bluetooth = DependencyService.Get<IBluetooth>();

            InitializeBluetoothCommand = new Command(async () =>
            {
                if (!bluetooth.IsBluetoothSupported())
                {
                    await CoreMethods.DisplayAlert("Bluetooth Not Supported",
                        "Bluetooth is not supported on this device.", "OK");
                    await CoreMethods.PopPageModel();
                }
                else
                {
                    PartnerDevices = new ObservableCollection<PartnerDevice>();
                    bluetooth.GetPairedDevices(PartnerDevices);
                    if (!await bluetooth.TurnOnBluetooth())
                    {
                        await CoreMethods.DisplayAlert("Bluetooth Not On",
                            "This device failed to turn on bluetooth.", "OK");
                    }
                    else
                    {
                        if (!await bluetooth.ListenForConnections())
                        {
                            await CoreMethods.DisplayAlert("Bluetooth Listening Failed",
                                "This device failed to begin listening for connections over bluetooth.", "OK");
                        }
                        else
                        {
                            await DisplayConnectionSuccessMessage();
                        }
                    }
                }
            });

            BecomeDiscoverableSecondaryCommand = new Command(async () =>
            {
                if (!await bluetooth.BecomeDiscoverable())
                {
                    await CoreMethods.DisplayAlert("Device Failed to Become Discoverable",
                        "This device failed to become discoverable over bluetooth.", "OK");
                }
            });

            SearchForDevicesPrimaryCommand = new Command(async () =>
            {
                AvailableDevices = new ObservableCollection<PartnerDevice>();
                if (!bluetooth.SearchForAvailableDevices(AvailableDevices))
                {
                    await CoreMethods.DisplayAlert("Device Failed to Search",
                        "This device failed to search for discoverable devices over bluetooth.", "OK");
                }
            });

            AttemptConnectionPrimaryCommand = new Command(async obj =>
            {
                if (!await bluetooth.AttemptConnection((PartnerDevice)obj))
                {
                    await CoreMethods.DisplayAlert("Device Failed to Connect",
                        "This devices failed to connect over bluetooth.", "OK");
                }
                else
                {
                    await DisplayConnectionSuccessMessage();
                }
            });

            ForgetDeviceCommand = new Command(obj =>
            {
                bluetooth.ForgetDevice((PartnerDevice)obj);
            });
        }

        private async Task DisplayConnectionSuccessMessage()
        {
            await CoreMethods.DisplayAlert("Connection Success", "Congrats!", "Yay");
        }

        protected override void ViewIsAppearing(object sender, EventArgs e)
        {
            base.ViewIsAppearing(sender, e);
            InitializeBluetoothCommand.Execute(null);
        }
    }
}