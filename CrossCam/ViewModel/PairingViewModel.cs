using CrossCam.Wrappers;
using FreshMvvm;
using Xamarin.Forms;

namespace CrossCam.ViewModel
{
    public class PairingViewModel : FreshBasePageModel
    {
        public bool HasBondedDevice { get; set; }
        public bool HasConnection { get; set; }
        public Command StartSearchingCommand { get; set; }
        public Command ListenForConnectionCommand { get; set; }
        public Command AttemptConnectionCommand { get; set; }

        public PairingViewModel()
        {
            var bluetooth = DependencyService.Get<IBluetooth>();

            StartSearchingCommand = new Command(async () =>
            {
                HasBondedDevice = await bluetooth.SearchForABondedDevice();
            });

            ListenForConnectionCommand = new Command(async () =>
            {
                HasConnection = await bluetooth.ListenForConnections();
            });

            AttemptConnectionCommand = new Command(() =>
            {
                HasConnection = bluetooth.AttemptConnection();
            });
        }
    }
}