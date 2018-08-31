using System;
using System.IO;
using Android.Graphics;
using CustomRenderer.CustomElement;
using CustomRenderer.Droid.CustomRenderer;
using Xamarin.Forms;

[assembly: Dependency(typeof(PhotoSaver))]
namespace CustomRenderer.Droid.CustomRenderer
{
    public class PhotoSaver : IPhotoSaver
    {
        public void SavePhoto(byte[] image)
        {
            var timeStamp = DateTime.Now.ToString("u");
            var imageFileName = "JPEG_" + timeStamp + "_";
            var sdCardPath = Android.OS.Environment.ExternalStorageDirectory.AbsolutePath;
            var filePath = System.IO.Path.Combine(sdCardPath, imageFileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                using (var bitmap = BitmapFactory.DecodeByteArray(image, 0, image.Length))
                {
                    bitmap.Compress(Bitmap.CompressFormat.Png, 100, stream);
                }
            }
        }
    }
}