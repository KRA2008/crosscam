﻿using CustomRenderer.CustomElement;
using CustomRenderer.iOS.CustomRenderer;
using Foundation;
using UIKit;
using Xamarin.Forms;

[assembly: Dependency(typeof(PhotoSaver))]
namespace CustomRenderer.iOS.CustomRenderer
{
    public class PhotoSaver : IPhotoSaver
    {
        public void SavePhoto(byte[] image)
        {
            var uiImage = UIImage.LoadFromData(NSData.FromArray(image));
            if (UIDevice.CurrentDevice.Orientation == UIDeviceOrientation.LandscapeRight)
            {
                using (var cgImage = uiImage.CGImage)
                {
                    uiImage = UIImage.FromImage(cgImage, 1, UIImageOrientation.Down);
                }
            }
            uiImage.SaveToPhotosAlbum((image1, error) =>
            {
                //uhm.
            });
            uiImage.Dispose();
        }
    }
}