using System;
using System.Linq;
using System.Threading.Tasks;
using CrossCam.iOS.CustomRenderer;
using CrossCam.Wrappers;
using Microsoft.AppCenter.Crashes;
using PhotosUI;
using UIKit;
using Xamarin.Forms;

[assembly: Dependency (typeof (PhotoPicker))]

namespace CrossCam.iOS.CustomRenderer
{
    public class PhotoPicker : IPhotoPicker
    {
        private TaskCompletionSource<byte[][]> _taskCompletionSource;
        private UIImagePickerController _imagePicker;
        private UIViewController _viewController;

        public Task<byte[][]> GetImages()
        {
            var window = UIApplication.SharedApplication.KeyWindow;
            _viewController = window.RootViewController;

            if (UIDevice.CurrentDevice.CheckSystemVersion(14, 0))
            {
                var phPicker = new PHPickerViewController(new PHPickerConfiguration
                {
                    Filter = PHPickerFilter.ImagesFilter,
                    SelectionLimit = 2
                })
                {
                    Delegate = new PHPickerDelegate(this)
                };

                _viewController.PresentModalViewController(phPicker, true);
            }
            else
            {
                _imagePicker = new UIImagePickerController
                {
                    SourceType = UIImagePickerControllerSourceType.PhotoLibrary,
                    MediaTypes = UIImagePickerController.AvailableMediaTypes(UIImagePickerControllerSourceType.PhotoLibrary)
                };

                _imagePicker.FinishedPickingMedia += OnImagePickerFinishedPickingMedia;
                _imagePicker.Canceled += OnImagePickerCancelled;

                _viewController.PresentModalViewController(_imagePicker, true);
            }

            _taskCompletionSource = new TaskCompletionSource<byte[][]>();
            return _taskCompletionSource.Task;
        }

        private void OnImagePickerFinishedPickingMedia(object sender, UIImagePickerMediaPickedEventArgs args)
        {
            var image = args.EditedImage ?? args.OriginalImage;

            if (image != null)
            {
                // Convert UIImage to .NET Stream object
                var data = image.AsJPEG(1).ToArray();

                UnregisterEventHandlers();

                // Set the Stream as the completion of the Task
                _taskCompletionSource.SetResult(new[] {data, null});
            }
            else
            {
                UnregisterEventHandlers();
                _taskCompletionSource.SetResult(null);
            }
            _imagePicker.DismissModalViewController(true);
        }

        private void OnImagePickerCancelled(object sender, EventArgs args)
        {
            UnregisterEventHandlers();
            _taskCompletionSource.SetResult(null);
            _imagePicker.DismissModalViewController(true);
        }

        private void UnregisterEventHandlers()
        {
            _imagePicker.FinishedPickingMedia -= OnImagePickerFinishedPickingMedia;
            _imagePicker.Canceled -= OnImagePickerCancelled;
        }

        private class PHPickerDelegate : PHPickerViewControllerDelegate
        {
            private readonly PhotoPicker _photoPicker;

            public PHPickerDelegate(PhotoPicker photoPicker)
            {
                _photoPicker = photoPicker;
            }

            public override async void DidFinishPicking(PHPickerViewController picker, PHPickerResult[] results)
            {
                try
                {
                    if (results.Length == 0)
                    {
                        _photoPicker._taskCompletionSource.SetResult(null);
                        _photoPicker._viewController.DismissModalViewController(true);
                        return;
                    }

                    var item1 = results.ElementAt(0).ItemProvider;
                    var identifier = item1.RegisteredTypeIdentifiers.First();
                    var data1 = await item1.LoadDataRepresentationAsync(identifier);
                    var bytes1 = data1.ToArray();

                    byte[] bytes2 = null;
                    if (results.Length == 2)
                    {
                        var data2 = await results.ElementAt(1).ItemProvider.LoadDataRepresentationAsync(identifier);
                        bytes2 = data2.ToArray();
                    }

                    _photoPicker._taskCompletionSource.SetResult(new[] {bytes1, bytes2});
                    _photoPicker._viewController.DismissModalViewController(true);
                }
                catch (Exception ex)
                {
                    Crashes.TrackError(ex);
                    _photoPicker._taskCompletionSource.SetResult(null);
                    _photoPicker._viewController.DismissModalViewController(true);
                }
            }
        }
    }

}