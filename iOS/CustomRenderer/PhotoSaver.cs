using System.IO;
using CustomRenderer.CustomElement;
using CustomRenderer.iOS.CustomRenderer;
using Foundation;
using UIKit;
using Xamarin.Forms;

[assembly: Dependency(typeof(PhotoSaver))]
namespace CustomRenderer.iOS.CustomRenderer
{
    public class PhotoSaver : IPhotoSaver
    {
        public void SavePhoto(Stream image)
        {
            var uiImage = UIImage.LoadFromData(NSData.FromStream(image));
            uiImage.SaveToPhotosAlbum((image1, error) =>
            {
                //uhm.
            });
        }
    }
}