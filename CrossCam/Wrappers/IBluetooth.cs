using System.Threading.Tasks;

namespace CrossCam.Wrappers
{
    public interface IBluetooth
    {
        Task<bool> SearchForABondedDevice();
        Task<bool> ListenForConnections();
        bool AttemptConnection();
    }
}