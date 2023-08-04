using System;
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
                var authStatus = await PHPhotoLibrary.RequestAuthorizationAsync(PHAccessLevel.ReadWrite);

                UIImage uiImage;
                if (authStatus == PHAuthorizationStatus.Authorized ||
                    authStatus == PHAuthorizationStatus.Limited)
                {
                    uiImage = new UIImage(NSData.FromArray(image));
                    var existingAlbum = GetCrossCamAlbum(saveInnerFolder); 
                    
                    if (existingAlbum == null)
                    {
                        var didAlbumCreationWork = PHPhotoLibrary.SharedPhotoLibrary.PerformChangesAndWait(() =>
                        {
                            PHAssetCollectionChangeRequest.CreateAssetCollection(saveInnerFolder);
                        }, out var albumCreationError);
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
                else
                {
                    authStatus = await PHPhotoLibrary.RequestAuthorizationAsync(PHAccessLevel.AddOnly);
                    if (authStatus == PHAuthorizationStatus.Authorized)
                    {
                        uiImage = new UIImage(NSData.FromArray(image));
                        SavePhotoIntoPhotos(uiImage, taskCompletionSource);
                    }
                    else
                    {
                        throw new Exception("Saving permissions not provided.");
                    }
                }

            }
            catch (Exception e)
            {
                taskCompletionSource.SetException(e);
            }

            return await taskCompletionSource.Task;
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
            var firstObject = collection.FirstObject;
            return firstObject as PHAssetCollection;
        }
    }
}