using System;
using System.Linq;
using System.Threading.Tasks;
using CrossCam.iOS.CustomRenderer;
using CrossCam.Wrappers;
using Foundation;
using Photos;
using UIKit;
using Xamarin.Forms;
// ReSharper disable HeuristicUnreachableCode

[assembly: Dependency(typeof(PhotoSaver))]
namespace CrossCam.iOS.CustomRenderer
{
    public class PhotoSaver : IPhotoSaver
    {
        public async Task<bool> SavePhoto(byte[] image, string saveOuterFolder, string saveInnerFolder, bool saveToSd)
        {
            var taskCompletionSource = new TaskCompletionSource<bool>();
            try
            {
                var uiImage = new UIImage(NSData.FromArray(image));

                PHAuthorizationStatus addOnlyAuthStatus;
                if (!UIDevice.CurrentDevice.CheckSystemVersion(15, 0))
                {
                    addOnlyAuthStatus = await PHPhotoLibrary.RequestAuthorizationAsync();

                    if (addOnlyAuthStatus == PHAuthorizationStatus.Authorized ||
                        addOnlyAuthStatus == PHAuthorizationStatus.Limited)
                    {
                        TryToFindAndSaveIntoAlbum(uiImage, saveInnerFolder, taskCompletionSource);
                    }
                    else
                    {
                        taskCompletionSource.SetException(new Exception("Save photos access not granted."));
                    }
                }
                else
                {
                    addOnlyAuthStatus = await PHPhotoLibrary.RequestAuthorizationAsync(PHAccessLevel.AddOnly);

                    if (addOnlyAuthStatus == PHAuthorizationStatus.Authorized ||
                        addOnlyAuthStatus == PHAuthorizationStatus.Limited)
                    {
                        var readWriteAuthStatus = await PHPhotoLibrary.RequestAuthorizationAsync(PHAccessLevel.ReadWrite);

                        if (readWriteAuthStatus == PHAuthorizationStatus.Authorized ||
                            readWriteAuthStatus == PHAuthorizationStatus.Limited)
                        {
                            TryToFindAndSaveIntoAlbum(uiImage, saveInnerFolder, taskCompletionSource);
                        }
                        else
                        {
                            SavePhotoIntoPhotos(uiImage, taskCompletionSource);
                        }
                    }
                    else
                    {
                        taskCompletionSource.SetException(new Exception("Save photos access not granted."));
                    }
                }
            }
            catch (Exception e)
            {
                taskCompletionSource.SetException(e);
            }

            return await taskCompletionSource.Task;
        }

        private static void TryToFindAndSaveIntoAlbum(UIImage uiImage, string saveInnerFolder, TaskCompletionSource<bool> taskCompletionSource)
        {
            var existingAlbum = GetCrossCamAlbum(saveInnerFolder);

            if (existingAlbum == null)
            {
                var didAlbumCreationWork = PHPhotoLibrary.SharedPhotoLibrary.PerformChangesAndWait(
                    () => { PHAssetCollectionChangeRequest.CreateAssetCollection(saveInnerFolder); },
                    out var albumCreationError);
                if (existingAlbum == null ||
                    didAlbumCreationWork &&
                    albumCreationError == null)
                {
                    existingAlbum = GetCrossCamAlbum(saveInnerFolder);
                    if (existingAlbum == null)
                    {
                        SavePhotoIntoPhotos(uiImage, taskCompletionSource);
                    }
                    else
                    {
                        SavePhotoIntoAlbum(uiImage, existingAlbum, taskCompletionSource);
                    }
                }
                else
                {
                    SavePhotoIntoPhotos(uiImage, taskCompletionSource);
                }
            }
            else
            {
                SavePhotoIntoAlbum(uiImage, existingAlbum, taskCompletionSource);
            }
        }

        private static void SavePhotoIntoPhotos(UIImage uiImage, TaskCompletionSource<bool> taskCompletionSource)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                uiImage.SaveToPhotosAlbum((image1, error) =>
                {
                    if (error != null)
                    {
                        throw new Exception(error.ToString());
                    }
                    else
                    {
                        taskCompletionSource.SetResult(true);
                    }
                });
            });
        }

        private static void SavePhotoIntoAlbum(UIImage uiImage, PHAssetCollection existingAlbum, TaskCompletionSource<bool> taskCompletionSource)
        {
            var saveIntoAlbumWorked = PHPhotoLibrary.SharedPhotoLibrary.PerformChangesAndWait(() =>
            {
                var assetRequest = PHAssetChangeRequest.FromImage(uiImage);
                var placeholder = assetRequest.PlaceholderForCreatedAsset;
                var albumRequest = PHAssetCollectionChangeRequest.ChangeRequest(existingAlbum);
                albumRequest.AddAssets(new PHObject[] { placeholder });
            }, out var imageSavingError);

            if (saveIntoAlbumWorked &&
                imageSavingError == null)
            {
                taskCompletionSource.SetResult(true);
            }
            else
            {
                throw new Exception(imageSavingError.ToString());
            }
        }

        private static PHAssetCollection GetCrossCamAlbum(string saveInnerFolder)
        {
            var fetchOptions = new PHFetchOptions
            {
                Predicate = NSPredicate.FromFormat("title=%@", new[] { NSObject.FromObject(saveInnerFolder) })
            };
            var collection = PHAssetCollection.FetchAssetCollections(PHAssetCollectionType.Album,
                PHAssetCollectionSubtype.AlbumRegular, fetchOptions);
            var firstObject = collection.FirstOrDefault();
            return firstObject as PHAssetCollection;
        }
    }
}