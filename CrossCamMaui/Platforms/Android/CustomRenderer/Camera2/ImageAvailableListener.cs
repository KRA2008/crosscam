using System;
using Android.Media;
using Image = Android.Media.Image;

namespace CrossCam.Droid.CustomRenderer.Camera2
{
    public class ImageAvailableListener : Java.Lang.Object, ImageReader.IOnImageAvailableListener
    {
        public event EventHandler<byte[]> Photo;
        public event EventHandler<Exception> Error;

        public void OnImageAvailable(ImageReader reader)
        {
            Image image = null;

            try
            {
                image = reader.AcquireNextImage();
                var buffer = image.GetPlanes()[0].Buffer;
                var imageData = new byte[buffer.Capacity()];
                buffer.Get(imageData);

                Photo?.Invoke(this, imageData);
            }
            catch (Exception e)
            {
                Error?.Invoke(this, e);
            }
            finally
            {
                image?.Close();
            }
        }
    }
}
