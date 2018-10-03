using System;
using System.IO;
using System.Threading.Tasks;
using CrossCam.Model;
using CrossCam.Wrappers;
using FreshMvvm;
using SkiaSharp;
using Xamarin.Forms;

namespace CrossCam.ViewModel
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
        
        public Command SaveCapturesCommand { get; set; }

        public Command ToggleViewModeCommand { get; set; }
        public bool IsViewMode { get; set; }

        public Command ClearCapturesCommand { get; set; }

        public Command NavigateToSettingsCommand { get; set; }

        public Settings Settings { get; set; }

        public bool IsViewPortrait { get; set; }

        public bool FailFadeTrigger { get; set; }
        public bool SuccessFadeTrigger { get; set; }
        public bool IsSaving { get; set; }

        public Aspect PreviewAspect => Settings.FillScreenPreview && !(IsViewMode && IsViewPortrait) ? Aspect.AspectFill : Aspect.AspectFit;

        public bool IsCaptureComplete => LeftByteArray != null && RightByteArray != null;
        public bool IsNothingCaptured => LeftByteArray == null && RightByteArray == null;
        public bool ShouldCaptureButtonBeVisible => !IsCaptureComplete && !IsSaving && !IsViewMode;
        public bool ShouldHelpTextBeVisible => IsNothingCaptured && !IsSaving && !IsViewMode;
        public bool ShouldLeftRetakeBeVisible => LeftByteArray != null && !IsSaving && !IsViewMode;
        public bool ShouldRightRetakeBeVisible => RightByteArray != null && !IsSaving && !IsViewMode;
        public bool ShouldEndButtonsBeVisible => IsCaptureComplete && !IsSaving && !IsViewMode;
        public bool ShouldSettingsBeVisible => IsNothingCaptured && !IsSaving && !IsViewMode;
        public bool ShouldLineGuidesBeVisible => (LeftByteArray == null ^ RightByteArray == null || Settings.ShowGuideLinesWithFirstCapture && !IsCaptureComplete) && Settings.AreGuideLinesVisible && !IsSaving && !IsViewMode;
        public bool ShouldDonutGuideBeVisible => (LeftByteArray == null ^ RightByteArray == null || Settings.ShowGuideLinesWithFirstCapture && !IsCaptureComplete) && Settings.IsGuideDonutVisible && !IsSaving && !IsViewMode;
        public bool ShouldPortraitWarningBeVisible => ShouldHelpTextBeVisible && IsViewPortrait;

        public string HelpText => "1) Frame up your subject in the center of the preview area" +
                                  "\n2) Take the left picture (but finish reading these directions first)" +
                                  "\n3) Move left as though the camera were mounted on a rail, with as little rotation as convenient on any axis" +
                                  "\n4) Start cross viewing with the preview that will have taken the place of these instructions" +
                                  "\n5) Guide lines will have appeared, align the right picture so the guide lines and the 3D image itself appear clear and sharp (you can drag the lines around if you wish)" +
                                  "\n6) Take the right picture when the desired level of 3D is achieved";

        public CameraViewModel()
        {
            var photoSaver = DependencyService.Get<IPhotoSaver>();
            IsLeftCameraVisible = true;

            Settings = PersistentStorage.LoadOrDefault(PersistentStorage.SETTINGS_KEY, new Settings());

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
                }
                else if (args.PropertyName == nameof(RightByteArray) &&
                         RightByteArray != null)
                {
                    RightImageSource = ImageSource.FromStream(() => new MemoryStream(RightByteArray));
                    IsRightCameraVisible = false;
                }
            };

            RetakeLeftCommand = new Command(() =>
            {
                IsRightCameraVisible = false;
                IsLeftCameraVisible = true;
                LeftByteArray = null;
                LeftImageSource = null;
            });

            RetakeRightCommand = new Command(() =>
            {
                if (!IsLeftCameraVisible)
                {
                    IsRightCameraVisible = true;
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

            NavigateToSettingsCommand = new Command(async () =>
            {
                await CoreMethods.PushPageModel<SettingsViewModel>(Settings);
            });

            SaveCapturesCommand = new Command(async () =>
            {
                IsSaving = true;
                LeftImageSource = null;
                RightImageSource = null;

                await Task.Delay(1); // take a break to go update the screen

                SKBitmap leftBitmap = null;
                SKBitmap rightBitmap = null;
                SKImage leftSkImage = null;
                SKImage rightSkImage = null;
                SKImage finalImage = null;
                byte[] finalJoinedImageBytes;
                byte[] rightFinalImageBytes;
                byte[] leftFinalImageBytes;
                var needs180Flip = false;
                try
                {
                    SKCodecOrigin origin;

                    using (var stream = new MemoryStream(LeftByteArray))
                    using (var data = SKData.Create(stream))
                    using (var codec = SKCodec.Create(data))
                    {
                        origin = codec.Origin;
                    }

                    switch (origin)
                    {
                        case SKCodecOrigin.BottomRight:
                            leftBitmap = BitmapRotate180(SKBitmap.Decode(LeftByteArray));
                            rightBitmap = BitmapRotate180(SKBitmap.Decode(RightByteArray));
                            needs180Flip = true;
                            break;
                        case SKCodecOrigin.RightTop:
                            leftBitmap = BitmapRotate90(SKBitmap.Decode(LeftByteArray));
                            rightBitmap = BitmapRotate90(SKBitmap.Decode(RightByteArray));
                            break;
                        default:
                            leftBitmap = SKBitmap.Decode(LeftByteArray);
                            rightBitmap = SKBitmap.Decode(RightByteArray);
                            break;
                    }
                    LeftByteArray = null;
                    RightByteArray = null;


                    double eachSideWidth;
                    if (leftBitmap.Height > leftBitmap.Width || !Settings.ClipLandscapeToFilledScreenPreview)
                    {
                        eachSideWidth = leftBitmap.Width;
                    }
                    else
                    {
                        var pictureHeightToScreenHeightRatio = leftBitmap.Height / Application.Current.MainPage.Height;
                        eachSideWidth = Application.Current.MainPage.Width * pictureHeightToScreenHeightRatio / 2d;
                    }

                    var imageLeftTrimWidth = (leftBitmap.Width - eachSideWidth) / 2d;

                    var floatedTrim = (float)imageLeftTrimWidth;
                    var floatedWidth = (float)eachSideWidth;

                    bool didSave;

                    if (!Settings.SaveSidesSeparately)
                    {
                        var finalImageWidth = eachSideWidth * 2;

                        using (var tempSurface = SKSurface.Create(new SKImageInfo((int)finalImageWidth, leftBitmap.Height)))
                        {
                            var canvas = tempSurface.Canvas;

                            canvas.Clear(SKColors.Transparent);

                            if (needs180Flip)
                            {
                                canvas.RotateDegrees(180);
                                canvas.Translate((float)(-1 * finalImageWidth), -1 * leftBitmap.Height);
                            }

                            canvas.DrawBitmap(leftBitmap,
                                SKRect.Create(floatedTrim, 0, floatedWidth, leftBitmap.Height),
                                SKRect.Create(0, 0, floatedWidth, leftBitmap.Height));
                            canvas.DrawBitmap(rightBitmap,
                                SKRect.Create(floatedTrim, 0, floatedWidth, leftBitmap.Height),
                                SKRect.Create(floatedWidth, 0, floatedWidth, leftBitmap.Height));


                            finalImage = tempSurface.Snapshot();
                        }

                        leftBitmap.Dispose();
                        rightBitmap.Dispose();

                        using (var encoded = finalImage.Encode(SKEncodedImageFormat.Jpeg, 100))
                        {
                            finalJoinedImageBytes = encoded.ToArray();
                        }

                        finalImage.Dispose();

                        didSave = await photoSaver.SavePhoto(finalJoinedImageBytes);
                    }
                    else
                    {
                        using (var tempsurface =
                            SKSurface.Create(new SKImageInfo((int) floatedWidth, leftBitmap.Height)))
                        {
                            var canvas = tempsurface.Canvas;

                            canvas.Clear(SKColors.Transparent);

                            canvas.DrawBitmap(leftBitmap,
                                SKRect.Create(floatedTrim, 0, floatedWidth, leftBitmap.Height),
                                SKRect.Create(0, 0, floatedWidth, leftBitmap.Height));

                            leftSkImage = tempsurface.Snapshot();

                            canvas.Clear(SKColors.Transparent);

                            canvas.DrawBitmap(rightBitmap,
                                SKRect.Create(floatedTrim, 0, floatedWidth, rightBitmap.Height),
                                SKRect.Create(0, 0, floatedWidth, rightBitmap.Height));

                            rightSkImage = tempsurface.Snapshot();

                        }
                        rightBitmap.Dispose();
                        leftBitmap.Dispose();

                        using (var encoded = leftSkImage.Encode(SKEncodedImageFormat.Jpeg, 100))
                        {
                            leftFinalImageBytes = encoded.ToArray();
                        }

                        didSave = await photoSaver.SavePhoto(leftFinalImageBytes);
                        leftFinalImageBytes = null; //TODO: need these? and the others?

                        using (var encoded = rightSkImage.Encode(SKEncodedImageFormat.Jpeg, 100))
                        {
                            rightFinalImageBytes = encoded.ToArray();
                        }

                        didSave = didSave && await photoSaver.SavePhoto(rightFinalImageBytes);
                        rightFinalImageBytes = null;
                    }

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
                    rightFinalImageBytes = null;
                    leftFinalImageBytes = null;
                    finalJoinedImageBytes = null;
                    rightSkImage?.Dispose();
                    leftSkImage?.Dispose();
                    finalImage?.Dispose();
                    leftBitmap?.Dispose();
                    rightBitmap?.Dispose();

                    IsSaving = false;
                    FailFadeTrigger = !FailFadeTrigger;
                }

                ClearCaptures();
            });
        }

        private static SKBitmap BitmapRotate90(SKBitmap originalBitmap)
        {
            var rotated = new SKBitmap(originalBitmap.Height, originalBitmap.Width);

            using (var surface = new SKCanvas(rotated))
            {
                surface.Translate(rotated.Width, 0);
                surface.RotateDegrees(90);
                surface.DrawBitmap(originalBitmap, 0, 0);
            }

            return rotated;
        }

        private static SKBitmap BitmapRotateNegative90(SKBitmap originalBitmap)
        {
            var rotated = new SKBitmap(originalBitmap.Height, originalBitmap.Width);

            using (var surface = new SKCanvas(rotated))
            {
                surface.Translate(0, rotated.Height);
                surface.RotateDegrees(-90);
                surface.DrawBitmap(originalBitmap, 0, 0);
            }

            return rotated;
        }

        private static SKBitmap BitmapRotate180(SKBitmap originalBitmap)
        {
            var rotated = new SKBitmap(originalBitmap.Width, originalBitmap.Height);

            using (var surface = new SKCanvas(rotated))
            {
                surface.Translate(rotated.Width, rotated.Height);
                surface.RotateDegrees(180);
                surface.DrawBitmap(originalBitmap, 0, 0);
            }

            return rotated;
        }

        protected override void ViewIsAppearing(object sender, EventArgs e)
        {
            base.ViewIsAppearing(sender, e);
            RaisePropertyChanged(nameof(ShouldLineGuidesBeVisible)); //TODO: figure out how to have Fody do this
            RaisePropertyChanged(nameof(ShouldDonutGuideBeVisible));
            RaisePropertyChanged(nameof(PreviewAspect));
        }

        private void ClearCaptures()
        {
            LeftByteArray = null;
            RightByteArray = null;
            LeftImageSource = null;
            RightImageSource = null;
            IsRightCameraVisible = false;
            IsLeftCameraVisible = true;
        }
    }
}