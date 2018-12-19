using System.Threading.Tasks;

namespace CrossCam.Wrappers
{
    public interface IPhotoPicker
    {
        Task<byte[]> GetImage();
    }
}