using System.Collections.Generic;
using System.Collections.ObjectModel;
using CrossCam.Wrappers;
using FreshMvvm;
using Xamarin.Forms;

namespace CrossCam.ViewModel
{
    public class PairingViewModel : FreshBasePageModel
    {
        public bool IsBluetoothSupported { get; set; }
        public Command CheckIfBluetoothIsSupportedCommand { get; set; }

        public bool IsBluetoothOn { get; set; }
        public Command TurnBluetoothOnCommand { get; set; }

        public List<PartnerDevice> PartnerDevices { get; set; }
        public Command GetPartnerDevicesCommand { get; set; }

        public bool IsSearchingForDevices { get; set; }
        public ObservableCollection<PartnerDevice> AvailableDevices { get; set; }
        public Command SearchForDevicesCommand { get; set; }

        public bool HasConnection { get; set; }
        public Command ListenForConnectionCommand { get; set; }
        public Command AttemptConnectionCommand { get; set; }

        public PairingViewModel()
        {
            var bluetooth = DependencyService.Get<IBluetooth>();

            CheckIfBluetoothIsSupportedCommand = new Command(() =>
            {
                IsBluetoothSupported = bluetooth.IsBluetoothSupported();
            });

            TurnBluetoothOnCommand = new Command(async () =>
            {
                IsBluetoothOn = await bluetooth.TurnOnBluetooth();
            });

            GetPartnerDevicesCommand = new Command(() =>
            {
                PartnerDevices = bluetooth.GetPairedDevices();
            });

            SearchForDevicesCommand = new Command(() =>
            {
                AvailableDevices = new ObservableCollection<PartnerDevice>();
                IsSearchingForDevices = bluetooth.SearchForAvailableDevices(AvailableDevices);
            });

            ListenForConnectionCommand = new Command(async () =>
            {
                HasConnection = await bluetooth.ListenForConnections();
            });

            AttemptConnectionCommand = new Command(async obj =>
            {
                var partnerDevice = (PartnerDevice) obj;
                HasConnection = await bluetooth.AttemptConnection(partnerDevice);
            });
        }
    }
}