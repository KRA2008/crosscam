using System;
using System.Threading.Tasks;
using CrossCam.iOS.CustomRenderer;
using CrossCam.Wrappers;
using Foundation;
using Photos;
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
                    using var cgImage = uiImage.CGImage;
                    uiImage = UIImage.FromImage(cgImage, 1, UIImageOrientation.Down);
                }

                var existingAlbum = GetCrossCamAlbum();
                if (existingAlbum == null)
                {
                    var didAlbumCreationWork = PHPhotoLibrary.SharedPhotoLibrary.PerformChangesAndWait(() =>
                    {
                        PHAssetCollectionChangeRequest.CreateAssetCollection("CrossCam");
                    }, out var albumCreationError);
                    if (existingAlbum == null ||
                        didAlbumCreationWork &&
                        albumCreationError == null)
                    {
                        existingAlbum = GetCrossCamAlbum();
                        if (existingAlbum == null)
                        {
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
                            return taskCompletionSource.Task;
                        }
                    }
                    else
                    {
                        taskCompletionSource.SetException(new Exception(albumCreationError.ToString()));
                        return taskCompletionSource.Task;
                    }
                }

                var imageSaveWorked = PHPhotoLibrary.SharedPhotoLibrary.PerformChangesAndWait(() =>
                {
                    var assetRequest = PHAssetChangeRequest.FromImage(uiImage);
                    var placeholder = assetRequest.PlaceholderForCreatedAsset;
                    var albumRequest = PHAssetCollectionChangeRequest.ChangeRequest(existingAlbum);
                    albumRequest.AddAssets(new PHObject[] { placeholder });
                }, out var imageSavingError);

                if (imageSaveWorked &&
                    imageSavingError == null)
                {
                    taskCompletionSource.SetResult(true);
                }
                else
                {
                    taskCompletionSource.SetException(new Exception(imageSavingError.ToString()));
                }
                return taskCompletionSource.Task;

            }
            catch (Exception e)
            {
                taskCompletionSource.SetException(e);
            }

            return taskCompletionSource.Task;
        }

        private static PHAssetCollection GetCrossCamAlbum()
        {
            var fetchOptions = new PHFetchOptions
            {
                Predicate = NSPredicate.FromFormat("title=%@", new[] { NSObject.FromObject("CrossCam") })
            };
            var collection = PHAssetCollection.FetchAssetCollections(PHAssetCollectionType.Album,
                PHAssetCollectionSubtype.AlbumRegular, fetchOptions);
            return collection.firstObject as PHAssetCollection;
        }
    }
}