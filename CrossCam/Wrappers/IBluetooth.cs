using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace CrossCam.Wrappers
{
    public interface IBluetooth
    {
        bool IsBluetoothSupported();
        Task<bool> TurnOnBluetooth();
        void GetPairedDevices(ObservableCollection<PartnerDevice> pairedDevices);
        bool SearchForAvailableDevices(ObservableCollection<PartnerDevice> partnerDevices);
        Task<bool> BecomeDiscoverable();
        Task<bool> ListenForConnections();
        Task<bool> AttemptConnection(PartnerDevice partnerDevice);
        void ForgetDevice(PartnerDevice partnerDevice);
    }

    public class PartnerDevice
    {
        public string Name { get; set; }
        public string Address { get; set; }
    }
}