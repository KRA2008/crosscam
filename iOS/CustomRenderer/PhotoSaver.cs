using System.Threading.Tasks;
using CrossCam.CustomElement;
using CustomRenderer.iOS.CustomRenderer;
using Foundation;
using UIKit;
using Xamarin.Forms;

[assembly: Dependency(typeof(PhotoSaver))]
namespace CustomRenderer.iOS.CustomRenderer
{
    public class PhotoSaver : IPhotoSaver
    {
        public Task<bool> SavePhoto(byte[] image)
        {
            var taskCompletionSource = new TaskCompletionSource<bool>();
            var uiImage = new UIImage(NSData.FromArray(image));
            if (UIDevice.CurrentDevice.Orientation == UIDeviceOrientation.LandscapeRight)
            {
                using (var cgImage = uiImage.CGImage)
                {
                    uiImage = UIImage.FromImage(cgImage, 1, UIImageOrientation.Down);
                }
            }
            uiImage.SaveToPhotosAlbum((image1, error) =>
            {
                taskCompletionSource.SetResult(error == null);
            });
            return taskCompletionSource.Task;
        }
    }
}