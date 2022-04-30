using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Android.Content;
using Android.Graphics;
using Android.Provider;
using AndroidX.DocumentFile.Provider;
using CrossCam.Droid.CustomRenderer;
using CrossCam.Wrappers;
using Java.Lang;
using Xamarin.Forms;
using Environment = Android.OS.Environment;
using Exception = System.Exception;
using Path = System.IO.Path;
using Uri = Android.Net.Uri;

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
                    var photoId = Guid.NewGuid().ToString("N");
                    var currentTimeSeconds = JavaSystem.CurrentTimeMillis() / 1000;

                    var externalDir = MainActivity.Instance?.GetExternalFilesDirs(Environment.DirectoryPictures)
                        ?.ElementAtOrDefault(1);
                    if (external && 
                        externalDir != null)
                    {
                        var externalPicturesDir = externalDir.AbsolutePath;
                        var newFilePath = Path.Combine(externalPicturesDir, photoId + ".jpg");
                        await using var stream = new FileStream(newFilePath, FileMode.CreateNew);
                        using var bitmap = await BitmapFactory.DecodeByteArrayAsync(image, 0, image.Length);
                        if (bitmap != null)
                        {
                            await bitmap.CompressAsync(Bitmap.CompressFormat.Jpeg, 100, stream);
                        }

                        using var file = new Java.IO.File(newFilePath);
                        MainActivity.Instance.SendBroadcast(new Intent(Intent.ActionMediaScannerScanFile,
                            Uri.FromFile(file)));
                    }
                    else
                    {
                        var contentResolver = MainActivity.Instance?.ContentResolver;
                        Uri destinationFinalUri;
                        if (destination != null)
                        {
                            var pickedDir = DocumentFile.FromTreeUri(MainActivity.Instance, Uri.Parse(destination));
                            var newFile = pickedDir.CreateFile("image/jpeg", photoId + ".jpg");
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
                            values.Put(MediaStore.Images.Media.InterfaceConsts.RelativePath, "Pictures/CrossCam");

                            destinationFinalUri = contentResolver?.Insert(MediaStore.Images.Media.ExternalContentUri, values);
                        }

                        if (destinationFinalUri != null)
                        {
                            await using var stream = contentResolver?.OpenOutputStream(destinationFinalUri);
                            using var bitmap = await BitmapFactory.DecodeByteArrayAsync(image, 0, image.Length);
                            if (bitmap != null)
                            {
                                await bitmap.CompressAsync(Bitmap.CompressFormat.Jpeg, 100, stream);
                            }
                        }

                        MainActivity.Instance?.SendBroadcast(new Intent(Intent.ActionMediaScannerScanFile,
                            destinationFinalUri));
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