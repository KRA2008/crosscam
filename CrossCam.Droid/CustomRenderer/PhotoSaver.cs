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
        public Task<bool> SavePhoto(byte[] image, string saveOuterFolder, string saveInnerFolder, bool saveToSd)
        {
            var taskCompletionSource = new TaskCompletionSource<bool>();

            Task.Run(async () =>
            {
                try
                {
                    var photoId = Guid.NewGuid().ToString("N");
                    var currentTimeSeconds = JavaSystem.CurrentTimeMillis() / 1000;

                    Uri destinationFinalUri;

                    if (saveToSd)
                    {
                        var externalPicturesDir = MainActivity.Instance.GetExternalFilesDirs(Environment.DirectoryPictures).ElementAt(1).AbsolutePath;
                        var newFilePath = Path.Combine(externalPicturesDir, currentTimeSeconds + ".jpg");
                        await using var stream = new FileStream(newFilePath, FileMode.CreateNew);
                        using var bitmap = BitmapFactory.DecodeByteArray(image, 0, image.Length);
                        await bitmap.CompressAsync(Bitmap.CompressFormat.Jpeg, 100, stream);

                        using var file = new Java.IO.File(newFilePath);
                        destinationFinalUri = Uri.FromFile(file);
                    }
                    else if (Android.OS.Build.VERSION.SdkInt <= Android.OS.BuildVersionCodes.P)
                    {
                        string targetFolderPath;
                        if (!string.IsNullOrWhiteSpace(saveOuterFolder))
                        {
                            targetFolderPath = Path.Combine(saveOuterFolder, saveInnerFolder);
                        }
                        else
                        {
                            var picturesFolder = new Java.IO.File(
                                Environment.GetExternalStoragePublicDirectory(Environment.DirectoryPictures),
                                saveInnerFolder ?? "");
                            if (!File.Exists(picturesFolder.AbsolutePath))
                            {
                                Directory.CreateDirectory(picturesFolder.AbsolutePath);
                            }

                            targetFolderPath = picturesFolder.AbsolutePath;
                        }

                        if (!Directory.Exists(targetFolderPath))
                        {
                            Directory.CreateDirectory(targetFolderPath);
                        }
                        
                        var newFilePath = Path.Combine(targetFolderPath, photoId + ".jpg");
                        await using var stream = new FileStream(newFilePath, FileMode.CreateNew);
                        using var bitmap = await BitmapFactory.DecodeByteArrayAsync(image, 0, image.Length);
                        await bitmap.CompressAsync(Bitmap.CompressFormat.Jpeg, 100, stream);

                        using var file = new Java.IO.File(newFilePath);
                        destinationFinalUri = Uri.FromFile(file);
                    }
                    else
                    {
                        var contentResolver = MainActivity.Instance?.ContentResolver;
                        if (!string.IsNullOrWhiteSpace(saveOuterFolder))
                        {
                            var pickedDir = DocumentFile.FromTreeUri(MainActivity.Instance, Uri.Parse(saveOuterFolder));
                            if (pickedDir == null ||
                                !pickedDir.Exists())
                            {
                                throw new DirectoryNotFoundException();
                            }

                            DocumentFile innerDir;
                            if (!string.IsNullOrWhiteSpace(saveInnerFolder))
                            {
                                innerDir = pickedDir.FindFile(saveInnerFolder);
                                if (innerDir == null ||
                                    !innerDir.Exists() ||
                                    !innerDir.IsDirectory)
                                {
                                    innerDir = pickedDir.CreateDirectory(saveInnerFolder);
                                }

                                if (innerDir == null ||
                                    !innerDir.Exists())
                                {
                                    throw new DirectoryNotFoundException();
                                }
                            }
                            else
                            {
                                innerDir = pickedDir;
                            }

                            var newFile = innerDir.CreateFile("image/jpeg", photoId + ".jpg");
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
                            values.Put(MediaStore.Images.Media.InterfaceConsts.RelativePath, "Pictures/" + saveInnerFolder);

                            destinationFinalUri = contentResolver?.Insert(MediaStore.Images.Media.ExternalContentUri, values);
                        }

                        await using var stream = contentResolver?.OpenOutputStream(destinationFinalUri);
                        using var bitmap = await BitmapFactory.DecodeByteArrayAsync(image, 0, image.Length);
                        await bitmap.CompressAsync(Bitmap.CompressFormat.Jpeg, 100, stream);
                    }

                    MainActivity.Instance?.SendBroadcast(new Intent(Intent.ActionMediaScannerScanFile,
                        destinationFinalUri));

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