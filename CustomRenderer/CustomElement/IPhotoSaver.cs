using System.Threading.Tasks;

namespace CustomRenderer.CustomElement
{
    public interface IPhotoSaver
    {
        Task<bool> SavePhoto(byte[] image);
    }
}