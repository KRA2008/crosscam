using System;
using Android.Content;
using Android.Graphics;
using Android.Provider;
using CustomRenderer.CustomElement;
using CustomRenderer.Droid.CustomRenderer;
using Java.Lang;
using Xamarin.Forms;

[assembly: Dependency(typeof(PhotoSaver))]
namespace CustomRenderer.Droid.CustomRenderer
{
    public class PhotoSaver : IPhotoSaver
    {
        public void SavePhoto(byte[] image)
        {
            var contentResolver = Android.App.Application.Context.ContentResolver;
            var values = new ContentValues();
            const string WHATEVER = "whatever";
            values.Put(MediaStore.Images.Media.InterfaceConsts.Title, WHATEVER);
            values.Put(MediaStore.Images.Media.InterfaceConsts.DisplayName, WHATEVER);
            values.Put(MediaStore.Images.Media.InterfaceConsts.Description, WHATEVER);
            values.Put(MediaStore.Images.Media.InterfaceConsts.MimeType, "image/jpeg");
            // Add the date meta data to ensure the image is added at the front of the gallery
            var currentTimeSeconds = JavaSystem.CurrentTimeMillis()/1000;
            values.Put(MediaStore.Images.Media.InterfaceConsts.DateAdded, currentTimeSeconds);
            values.Put(MediaStore.Images.Media.InterfaceConsts.DateModified, currentTimeSeconds);

            var url = contentResolver.Insert(MediaStore.Images.Media.ExternalContentUri, values);

            var imageOut = contentResolver.OpenOutputStream(url);
            try
            {
                using (var bitmap = BitmapFactory.DecodeByteArray(image, 0, image.Length))
                {
                    bitmap.Compress(Bitmap.CompressFormat.Jpeg, 100, imageOut);
                }
            }
            finally
            {
                imageOut.Close();
            }

            //    var id = ContentUris.ParseId(url);
            //    // Wait until MINI_KIND thumbnail is generated.
            //    Bitmap miniThumb = MediaStore.Images.Thumbnails.getThumbnail(contentResolver, id, MediaStore.Images.Thumbnails.MINI_KIND, null);
            //    // This is for backward compatibility.
            //    storeThumbnail(contentResolver, miniThumb, id, 50F, 50F, MediaStore.Images.Thumbnails.MICRO_KIND);
            //}
            //catch (Exception e)
            //{
            //    if (url != null)
            //    {
            //        contentResolver.delete(url, null, null);
            //        url = null;
            //    }
            //}

            //if (url != null)
            //{
            //    stringUrl = url.toString();
            //}

            //return stringUrl;
        }
    }
}