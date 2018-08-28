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
            if (UIDevice.CurrentDevice.Orientation == UIDeviceOrientation.LandscapeRight)
            {
                var cgImage = uiImage.CGImage;
                uiImage = UIImage.FromImage(cgImage, 1, UIImageOrientation.Down);
                cgImage.Dispose();
            }
            uiImage.SaveToPhotosAlbum((image1, error) =>
            {
                //uhm.
            });
            uiImage.Dispose();
        }
    }
}