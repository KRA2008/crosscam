using System.IO;
using System.Threading.Tasks;
using CustomRenderer.CustomElement;
using FreshMvvm;
using SkiaSharp;
using Xamarin.Forms;

namespace CustomRenderer.ViewModel
{
    public sealed class CameraViewModel : FreshBasePageModel
    {
        public ImageSource LeftImageSource { get; set; }
        public byte[] LeftByteArray { get; set; }
        public bool IsLeftCameraVisible { get; set; }
        public Command RetakeLeftCommand { get; set; }
        public bool LeftCaptureSuccess { get; set; }

        public ImageSource RightImageSource { get; set; }
        public byte[] RightByteArray { get; set; }
        public bool IsRightCameraVisible { get; set; }
        public Command RetakeRightCommand { get; set; }
        public bool RightCaptureSuccess { get; set; }

        public Command CapturePictureCommand { get; set; }
        public bool CapturePictureTrigger { get; set; }

        public bool IsCaptureComplete { get; set; }
        public Command SaveCapturesCommand { get; set; }

        public Command ToggleViewModeCommand { get; set; }
        public bool IsViewMode { get; set; }

        public Command ClearCapturesCommand { get; set; }

        public bool FailFadeTrigger { get; set; }
        public bool SuccessFadeTrigger { get; set; }
        public bool IsSaving { get; set; }

        public string HelpText => "1) Put the donut on a recognizable point (drag it around if you wish)" +
                                  "\n2) Take the left picture" +
                                  "\n3) Start cross viewing" +
                                  "\n4) Move left" +
                                  "\n5) Adjust the depth by moving more or less" +
                                  "\n6) Align by putting the dot in the donut while cross viewing" +
                                  "\n7) Take the right picture" +
                                  "\n\nTips:" +
                                  "\n - keep the camera level" +
                                  "\n - keep the camera at the same height" +
                                  "\n - some would say to put the donut on a background point";

        public CameraViewModel()
        {
            var photoSaver = DependencyService.Get<IPhotoSaver>();
            IsLeftCameraVisible = true;

            PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(LeftByteArray) &&
                    LeftByteArray != null)
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
                else if (args.PropertyName == nameof(RightByteArray) &&
                         RightByteArray != null)
                {
                    RightImageSource = ImageSource.FromStream(() => new MemoryStream(RightByteArray));
                    IsRightCameraVisible = false;
                    IsCaptureComplete = true;
                }
            };

            RetakeLeftCommand = new Command(() =>
            {
                IsRightCameraVisible = false;
                IsLeftCameraVisible = true;
                IsCaptureComplete = false;
                LeftByteArray = null;
                LeftImageSource = null;
            });

            RetakeRightCommand = new Command(() =>
            {
                if (!IsLeftCameraVisible)
                {
                    IsRightCameraVisible = true;
                    IsCaptureComplete = false;
                    RightByteArray = null;
                    RightImageSource = null;
                }
            });

            ClearCapturesCommand = new Command(ClearCaptures);

            CapturePictureCommand = new Command(() =>
            {
                CapturePictureTrigger = !CapturePictureTrigger;
            });

            ToggleViewModeCommand = new Command(() =>
            {
                IsViewMode = !IsViewMode;
            });

            SaveCapturesCommand = new Command(async () =>
            {
                IsSaving = true;
                LeftImageSource = null;
                RightImageSource = null;

                await Task.Delay(500); // breathing room for screen to update

                SKBitmap leftBitmap = null;
                SKBitmap rightBitmap = null;
                SKImage finalImage = null;
                try
                {
                    leftBitmap = SKBitmap.Decode(LeftByteArray);
                    LeftByteArray = null;

                    rightBitmap = SKBitmap.Decode(RightByteArray);
                    RightByteArray = null;

                    var screenHeight = (int)Application.Current.MainPage.Height;
                    var screenWidth = (int)Application.Current.MainPage.Width;

                    var pictureHeightToScreenHeightRatio = (float) leftBitmap.Height / screenHeight;

                    var eachSideWidth = screenWidth * pictureHeightToScreenHeightRatio / 2f;
                    var imageLeftTrimWidth = (leftBitmap.Width - eachSideWidth) / 2f;

                    var finalImageWidth = eachSideWidth * 2;

                    using (var tempSurface = SKSurface.Create(new SKImageInfo((int)finalImageWidth, leftBitmap.Height)))
                    {
                        var canvas = tempSurface.Canvas;
                        
                        canvas.Clear(SKColors.Transparent);

                        canvas.DrawBitmap(leftBitmap,
                            SKRect.Create(imageLeftTrimWidth, 0, eachSideWidth, leftBitmap.Height),
                            SKRect.Create(0, 0, eachSideWidth, leftBitmap.Height));
                        canvas.DrawBitmap(rightBitmap,
                            SKRect.Create(imageLeftTrimWidth, 0, eachSideWidth, leftBitmap.Height),
                            SKRect.Create(eachSideWidth, 0, eachSideWidth, leftBitmap.Height));

                        finalImage = tempSurface.Snapshot();
                    }

                    byte[] finalImageByteArray;
                    using (var encoded = finalImage.Encode(SKEncodedImageFormat.Jpeg, 100))
                    {
                        finalImageByteArray = encoded.ToArray();
                    }

                    finalImage.Dispose();
                    leftBitmap.Dispose();
                    rightBitmap.Dispose();

                    var didSave = await photoSaver.SavePhoto(finalImageByteArray);
                    IsSaving = false;

                    if (didSave)
                    {
                        SuccessFadeTrigger = !SuccessFadeTrigger;
                    }
                    else
                    {
                        FailFadeTrigger = !FailFadeTrigger;
                    }
                }
                catch
                {
                    finalImage?.Dispose();
                    leftBitmap?.Dispose();
                    rightBitmap?.Dispose();
                }

                ClearCaptures();
            });
        }

        private void ClearCaptures()
        {
            LeftByteArray = null;
            RightByteArray = null;
            LeftImageSource = null;
            RightImageSource = null;
            IsCaptureComplete = false;
            IsRightCameraVisible = false;
            IsLeftCameraVisible = true;
        }
    }
}