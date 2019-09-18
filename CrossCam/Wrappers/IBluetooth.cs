using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace CrossCam.Wrappers
{
    public interface IBluetooth
    {
        bool IsBluetoothSupported();
        Task<bool> TurnOnBluetooth();
        List<PartnerDevice> GetPairedDevices();
        bool SearchForAvailableDevices(ObservableCollection<PartnerDevice> partnerDevices);
        Task<bool> ListenForConnections();
        Task<bool> AttemptConnection(PartnerDevice partnerDevice);
    }

    public class PartnerDevice
    {
        public string Name { get; set; }
        public string Address { get; set; }
    }
}