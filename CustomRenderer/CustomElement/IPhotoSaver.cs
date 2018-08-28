using System.IO;

namespace CustomRenderer.CustomElement
{
    public interface IPhotoSaver
    {
        void SavePhoto(Stream image);
    }
}