using System.Threading.Tasks;

namespace CrossCam.Wrappers
{
    public interface IPlatformBluetooth
    {
        Task<bool> RequestBluetoothPermissions();
        Task<bool> RequestLocationPermissions();
        bool IsBluetoothSupported();
        bool IsBluetoothApiLevelSufficient();
        Task<bool> TurnOnBluetooth();
        Task<bool> TurnOnLocationServices();
    }
}