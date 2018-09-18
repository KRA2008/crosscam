using System.Threading.Tasks;
using Android.Content;
using Android.Graphics;
using Android.Provider;
using CrossCam.Droid.CustomRenderer;
using CrossCam.Wrappers;
using Java.Lang;
using Xamarin.Forms;

[assembly: Dependency(typeof(PhotoSaver))]
namespace CrossCam.Droid.CustomRenderer
{
    public class PhotoSaver : IPhotoSaver
    {
        public Task<bool> SavePhoto(byte[] image)
        {
            var taskCompletionSource = new TaskCompletionSource<bool>();

            Task.Run(() =>
            {
                try
                {
                    var contentResolver = Android.App.Application.Context.ContentResolver;
                    var values = new ContentValues();
                    values.Put(MediaStore.Images.Media.InterfaceConsts.MimeType, "image/jpeg");

                    var currentTimeSeconds = JavaSystem.CurrentTimeMillis() / 1000;
                    values.Put(MediaStore.Images.Media.InterfaceConsts.DateAdded, currentTimeSeconds);
                    values.Put(MediaStore.Images.Media.InterfaceConsts.DateModified, currentTimeSeconds);

                    var url = contentResolver.Insert(MediaStore.Images.Media.ExternalContentUri, values);

                    using (var imageOut = contentResolver.OpenOutputStream(url))
                    {
                        using (var bitmap = BitmapFactory.DecodeByteArray(image, 0, image.Length))
                        {
                            bitmap.Compress(Bitmap.CompressFormat.Jpeg, 100, imageOut);
                        }
                    }
                }
                catch
                {
                    taskCompletionSource.SetResult(false);
                }
                taskCompletionSource.SetResult(true);
            });

            return taskCompletionSource.Task;
        }
    }
}