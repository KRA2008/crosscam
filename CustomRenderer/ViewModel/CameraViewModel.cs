using System.IO;
using FreshMvvm;
using Xamarin.Forms;

namespace CustomRenderer.ViewModel
{
    public sealed class CameraViewModel : FreshBasePageModel
    {
        public ImageSource LeftImageSource { get; set; }
        public byte[] LeftByteArray { get; set; }
        public bool IsLeftCameraVisible { get; set; }
        public Command RetakeLeftCommand { get; set; }

        public ImageSource RightImageSource { get; set; }
        public byte[] RightByteArray { get; set; }
        public bool IsRightCameraVisible { get; set; }
        public Command RetakeRightCommand { get; set; }

        public Command CapturePictureCommand { get; set; }
        public bool CapturePictureTrigger { get; set; }

        public bool IsCaptureComplete { get; set; }
        public Command SaveCaptures { get; set; }

        public CameraViewModel()
        {
            IsLeftCameraVisible = true;

            PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(LeftByteArray))
                {
                    LeftImageSource = ImageSource.FromStream(() => new MemoryStream(LeftByteArray));
                    IsLeftCameraVisible = false;
                    if (RightByteArray == null)
                    {
                        IsRightCameraVisible = true;
                    }
                    else
                    {
                        IsCaptureComplete = true;
                    }
                }
                else if (args.PropertyName == nameof(RightByteArray))
                {
                    RightImageSource = ImageSource.FromStream(() => new MemoryStream(RightByteArray));
                    IsRightCameraVisible = false;
                    IsCaptureComplete = true;
                }
            };

            RetakeLeftCommand = new Command(() =>
            {
                if (!IsRightCameraVisible)
                {
                    IsLeftCameraVisible = true;
                    IsCaptureComplete = false;
                }
            });

            RetakeRightCommand = new Command(() =>
            {
                if (!IsLeftCameraVisible)
                {
                    IsRightCameraVisible = true;
                    IsCaptureComplete = false;
                }
            });

            CapturePictureCommand = new Command(() =>
            {
                CapturePictureTrigger = !CapturePictureTrigger;
            });

            SaveCaptures = new Command(async () =>
            {
                await CoreMethods.PushPageModel<RenderViewModel>(new[] {LeftByteArray, RightByteArray});
            });
        }
    }
}