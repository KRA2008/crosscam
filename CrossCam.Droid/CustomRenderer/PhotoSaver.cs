using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Android.Content;
using Android.Graphics;
using Android.Net;
using Android.OS;
using Android.Provider;
using Android.Support.V4.Provider;
using CrossCam.Droid.CustomRenderer;
using CrossCam.Wrappers;
using Java.Lang;
using Xamarin.Forms;
using Exception = System.Exception;
using Path = System.IO.Path;

[assembly: Dependency(typeof(PhotoSaver))]
namespace CrossCam.Droid.CustomRenderer
{
    public class PhotoSaver : IPhotoSaver
    {
        public Task<bool> SavePhoto(byte[] image, string destination, bool external)
        {
            var taskCompletionSource = new TaskCompletionSource<bool>();

            Task.Run(async () =>
            {
                try
                {
                    var contentResolver = MainActivity.Instance.ContentResolver;

                    var currentTimeSeconds = JavaSystem.CurrentTimeMillis() / 1000;

                    if (external)
                    {
                        var externalPicturesDir = MainActivity.Instance.GetExternalFilesDirs(Environment.DirectoryPictures).ElementAt(1).AbsolutePath;
                        var newFilePath = Path.Combine(externalPicturesDir, currentTimeSeconds + ".jpg");
                        using (var stream = new FileStream(newFilePath, FileMode.CreateNew))
                        {
                            using (var bitmap = BitmapFactory.DecodeByteArray(image, 0, image.Length))
                            {
                                await bitmap.CompressAsync(Bitmap.CompressFormat.Jpeg, 100, stream);
                            }
                        }

                        using (var file = new Java.IO.File(newFilePath))
                        {
                            MainActivity.Instance.SendBroadcast(new Intent(Intent.ActionMediaScannerScanFile, Uri.FromFile(file)));
                        }
                    }
                    else
                    {
                        Uri destinationFinalUri;
                        if (destination != null)
                        {
                            var pickedDir = DocumentFile.FromTreeUri(MainActivity.Instance, Uri.Parse(destination));
                            var newFile = pickedDir.CreateFile("image/jpeg", currentTimeSeconds + ".jpg");
                            if (newFile == null)
                            {
                                throw new DirectoryNotFoundException();
                            }
                            destinationFinalUri = newFile.Uri;
                        }
                        else
                        {
                            var values = new ContentValues();
                            values.Put(MediaStore.Images.Media.InterfaceConsts.MimeType, "image/jpeg");
                            values.Put(MediaStore.Images.Media.InterfaceConsts.DateAdded, currentTimeSeconds);
                            values.Put(MediaStore.Images.Media.InterfaceConsts.DateModified, currentTimeSeconds);

                            destinationFinalUri = contentResolver.Insert(MediaStore.Images.Media.ExternalContentUri, values);
                        }

                        using (var stream = contentResolver.OpenOutputStream(destinationFinalUri))
                        {
                            using (var bitmap = BitmapFactory.DecodeByteArray(image, 0, image.Length))
                            {
                                await bitmap.CompressAsync(Bitmap.CompressFormat.Jpeg, 100, stream);
                            }
                        }

                        MainActivity.Instance.SendBroadcast(new Intent(Intent.ActionMediaScannerScanFile, destinationFinalUri));
                    }

                    taskCompletionSource.SetResult(true);
                }
                catch (Exception e)
                {
                    taskCompletionSource.SetException(e);
                }
            });

            return taskCompletionSource.Task;
        }
    }
}