using System;
using System.Threading.Tasks;
using CrossCam.iOS.CustomRenderer;
using CrossCam.Wrappers;
using Foundation;
using UIKit;
using Xamarin.Forms;

[assembly: Dependency(typeof(PhotoSaver))]
namespace CrossCam.iOS.CustomRenderer
{
    public class PhotoSaver : IPhotoSaver
    {
        public Task<bool> SavePhoto(byte[] image, string destination, bool external)
        {
            var taskCompletionSource = new TaskCompletionSource<bool>();

            try
            {
                var uiImage = new UIImage(NSData.FromArray(image));
                if (UIDevice.CurrentDevice.Orientation == UIDeviceOrientation.LandscapeRight)
                {
                    using (var cgImage = uiImage.CGImage)
                    {
                        uiImage = UIImage.FromImage(cgImage, 1, UIImageOrientation.Down);
                    }
                }

                Device.BeginInvokeOnMainThread(() =>
                {
                    uiImage.SaveToPhotosAlbum((image1, error) =>
                    {
                        if (error != null)
                        {
                            taskCompletionSource.SetException(new Exception(error.ToString()));
                        }
                        else
                        {
                            taskCompletionSource.SetResult(true);
                        }
                    });
                });
            }
            catch (Exception e)
            {
                taskCompletionSource.SetException(e);
            }

            return taskCompletionSource.Task;
        }
    }
}