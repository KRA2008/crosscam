using System.Threading.Tasks;

namespace CrossCam.Wrappers
{
    public interface IBluetooth
    {
        Task<bool> RequestBluetoothPermissions();
        Task<bool> RequestLocationPermissions();
        bool IsBluetoothSupported();
        bool IsBluetoothApiLevelSufficient();
        Task<bool> TurnOnBluetooth();
        Task<bool> TurnOnLocationServices();
        Task<bool> SendPreviewFrame(byte[] preview);
        Task<byte[]> Capture(int countdownSeconds);
    }
}