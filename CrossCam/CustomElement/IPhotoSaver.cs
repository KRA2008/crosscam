using System.Threading.Tasks;

namespace CrossCam.CustomElement
{
    public interface IPhotoSaver
    {
        Task<bool> SavePhoto(byte[] image);
    }
}