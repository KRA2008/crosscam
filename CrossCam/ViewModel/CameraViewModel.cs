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
        public byte[] LeftByteArray { get; set; }
        public Command RetakeLeftCommand { get; set; }
        public bool LeftCaptureSuccess { get; set; }

        public byte[] RightByteArray { get; set; }
        public Command RetakeRightCommand { get; set; }
        public bool RightCaptureSuccess { get; set; }

        public ImageSource FirstImageSource { get; set; }
        public int FirstImageColumn => IsCaptureLeftFirst ? 0 : 1;
        public ImageSource SecondImageSource { get; set; }
        public int SecondImageColumn => IsCaptureLeftFirst ? 1 : 0;

        public bool IsCameraVisible { get; set; }
        public byte[] CapturedImageBytes { get; set; }
        public bool CaptureSuccess { get; set; }
        public int CameraColumn { get; set; }

        public Command CapturePictureCommand { get; set; }
        public bool CapturePictureTrigger { get; set; }

        public bool MoveLeftTrigger { get; set; }
        public bool MoveRightTrigger { get; set; }

        public Command SaveCapturesCommand { get; set; }

        public Command ToggleViewModeCommand { get; set; }
        public bool IsViewMode { get; set; }

        public Command ClearCapturesCommand { get; set; }

        public Command NavigateToSettingsCommand { get; set; }
        public Command NavigateToHelpCommand { get; set; }

        public Command SwapSidesCommand { get; set; }

        public Command PromptForPermissionAndSendErrorEmailCommand { get; set; }
        public string ErrorMessage { get; set; }

        public Settings Settings { get; set; }

        public bool IsViewPortrait { get; set; }
        public bool IsCaptureLeftFirst { get; set; }

        public bool FailFadeTrigger { get; set; }
        public bool SuccessFadeTrigger { get; set; }
        public bool IsSaving { get; set; }

        public bool SwitchToContinuousFocusTrigger { get; set; }

        public Aspect PreviewAspect => Settings.FillScreenPreview && !(IsViewMode && IsViewPortrait) ? Aspect.AspectFill : Aspect.AspectFit;

        public bool IsCaptureComplete => LeftByteArray != null && RightByteArray != null;
        public bool IsNothingCaptured => LeftByteArray == null && RightByteArray == null;
        public bool ShouldCaptureButtonBeVisible => !IsCaptureComplete && !IsSaving && !IsViewMode;
        public bool ShouldHelpTextBeVisible => IsNothingCaptured && !IsSaving && !IsViewMode && HelpTextColumn != CameraColumn;
        public bool ShouldLeftRetakeBeVisible => LeftByteArray != null && !IsSaving && !IsViewMode;
        public bool ShouldRightRetakeBeVisible => RightByteArray != null && !IsSaving && !IsViewMode;
        public bool ShouldEndButtonsBeVisible => IsCaptureComplete && !IsSaving && !IsViewMode;
        public bool ShouldViewButtonBeVisible => ShouldEndButtonsBeVisible && (!IsViewPortrait || Settings.FillScreenPreview);
        public bool ShouldSettingsAndInfoBeVisible => IsNothingCaptured && !IsSaving && !IsViewMode;
        public bool ShouldLineGuidesBeVisible => (LeftByteArray == null ^ RightByteArray == null || Settings.ShowGuideLinesWithFirstCapture && !IsCaptureComplete) && Settings.AreGuideLinesVisible && !IsSaving && !IsViewMode;
        public bool ShouldDonutGuideBeVisible => (LeftByteArray == null ^ RightByteArray == null || Settings.ShowGuideDonutWithFirstCapture && !IsCaptureComplete) && Settings.IsGuideDonutVisible && !IsSaving && !IsViewMode;
        public bool ShouldPortraitWarningBeVisible => ShouldHelpTextBeVisible && IsViewPortrait;

        public string HelpText => "1) Frame up your subject" +
                                  "\n2) Take the first picture (but finish reading these directions first)" +
                                  "\n3) Move " + SlideDirection + "" +
                                  "\n4) Start cross viewing with the preview that will have taken the place of these instructions" +
                                  "\n5) Guide lines will have appeared, align the second picture so the guide lines and the 3D image itself appear clear and sharp" +
                                  "\n6) Take the second picture when the desired level of 3D is achieved";
        public string SlideDirection => IsCaptureLeftFirst ? "LEFT" : "RIGHT";
        public int HelpTextColumn => IsCaptureLeftFirst ? 1 : 0;

        public CameraViewModel()
        {
            var photoSaver = DependencyService.Get<IPhotoSaver>();
            IsCameraVisible = true;

            Settings = PersistentStorage.LoadOrDefault(PersistentStorage.SETTINGS_KEY, new Settings());

            IsCaptureLeftFirst = Settings.IsCaptureLeftFirst;
            CameraColumn = IsCaptureLeftFirst ? 0 : 1;

            PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(CaptureSuccess))
                {
                    if (CameraColumn == 0)
                    {
                        LeftCaptureSuccess = !LeftCaptureSuccess;
                    }
                    else
                    {
                        RightCaptureSuccess = !RightCaptureSuccess;
                    }
                }
                else if (args.PropertyName == nameof(CapturedImageBytes))
                {
                    if (CameraColumn == 0)
                    {
                        LeftByteArray = CapturedImageBytes;

                        if (IsCaptureLeftFirst)
                        {
                            FirstImageSource = ImageSource.FromStream(() => new MemoryStream(LeftByteArray));
                        }
                        else
                        {
                            SecondImageSource = ImageSource.FromStream(() => new MemoryStream(LeftByteArray));
                        }

                        if (RightByteArray == null)
                        {
                            MoveLeftTrigger = !MoveLeftTrigger;
                            CameraColumn = 1;
                        }
                        else
                        {
                            CameraColumn = IsCaptureLeftFirst ? 0 : 1;
                            IsCameraVisible = false;
                        }
                    }
                    else
                    {
                        RightByteArray = CapturedImageBytes;

                        if (IsCaptureLeftFirst)
                        {
                            SecondImageSource = ImageSource.FromStream(() => new MemoryStream(RightByteArray));
                        }
                        else
                        {
                            FirstImageSource = ImageSource.FromStream(() => new MemoryStream(RightByteArray));
                        }

                        if (LeftByteArray == null)
                        {
                            MoveRightTrigger = !MoveRightTrigger;
                            CameraColumn = 0;
                        }
                        else
                        {
                            CameraColumn = IsCaptureLeftFirst ? 0 : 1;
                            IsCameraVisible = false;
                        }
                    }
                }
                else if (args.PropertyName == nameof(ErrorMessage))
                {
                    if (ErrorMessage != null)
                    {
                        PromptForPermissionAndSendErrorEmailCommand.Execute(null);
                    }
                }
            };

            RetakeLeftCommand = new Command(() =>
            {
                CameraColumn = 0;
                IsCameraVisible = true;
                LeftByteArray = null;
                if (IsCaptureLeftFirst)
                {
                    FirstImageSource = null;
                }
                else
                {
                    SecondImageSource = null;
                }
                if (RightByteArray != null)
                {
                    MoveRightTrigger = !MoveRightTrigger;
                }
            });

            RetakeRightCommand = new Command(() =>
            {
                CameraColumn = 1;
                IsCameraVisible = true;
                RightByteArray = null;
                if (IsCaptureLeftFirst)
                {
                    SecondImageSource = null;
                }
                else
                {
                    FirstImageSource = null;
                }
                if (LeftByteArray != null)
                {
                    MoveLeftTrigger = !MoveLeftTrigger;
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

            NavigateToHelpCommand = new Command(async () =>
            {
                await CoreMethods.PushPageModel<HelpViewModel>();
            });

            SwapSidesCommand = new Command(() =>
            {
                IsCaptureLeftFirst = !IsCaptureLeftFirst;

                var tempArray = LeftByteArray;
                LeftByteArray = RightByteArray;
                RightByteArray = tempArray;

                if (IsCameraVisible)
                {
                    CameraColumn = CameraColumn == 0 ? 1 : 0;
                }

                if (LeftByteArray != null &&
                    RightByteArray == null)
                {
                    MoveLeftTrigger = !MoveLeftTrigger;
                }

                if (LeftByteArray == null &&
                    RightByteArray != null)
                {
                    MoveRightTrigger = !MoveRightTrigger;
                }

                Settings.IsCaptureLeftFirst = IsCaptureLeftFirst;
                PersistentStorage.Save(PersistentStorage.SETTINGS_KEY, Settings);
            });

            SaveCapturesCommand = new Command(async () =>
            {
                IsSaving = true;
                FirstImageSource = null;
                SecondImageSource = null;

                await Task.Delay(100); // take a break to go update the screen

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
                        if (IsViewPortrait)
                        {
                            eachSideWidth = Application.Current.MainPage.Height / 2f * leftBitmap.Height / Application.Current.MainPage.Width;
                        }
                        else
                        {
                            eachSideWidth = Application.Current.MainPage.Width / 2f * leftBitmap.Height / Application.Current.MainPage.Height;
                        }
                    }

                    var imageLeftTrimWidth = (leftBitmap.Width - eachSideWidth) / 2d;

                    var floatedTrim = (float) imageLeftTrimWidth;
                    var floatedWidth = (float) eachSideWidth;

                    bool didSave;

                    if (!Settings.SaveSidesSeparately)
                    {
                        var finalImageWidth = eachSideWidth * 2;

                        using (var tempSurface =
                            SKSurface.Create(new SKImageInfo((int) finalImageWidth, leftBitmap.Height)))
                        {
                            var canvas = tempSurface.Canvas;

                            canvas.Clear(SKColors.Transparent);

                            if (needs180Flip)
                            {
                                canvas.RotateDegrees(180);
                                canvas.Translate((float) (-1 * finalImageWidth), -1 * leftBitmap.Height);
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
                catch (Exception e)
                {
                    ErrorMessage = e.ToString();

                    IsSaving = false;

                    FailFadeTrigger = !FailFadeTrigger;
                }
                finally
                {
                    rightFinalImageBytes = null;
                    leftFinalImageBytes = null;
                    finalJoinedImageBytes = null;
                    rightSkImage?.Dispose();
                    leftSkImage?.Dispose();
                    finalImage?.Dispose();
                    leftBitmap?.Dispose();
                    rightBitmap?.Dispose();

                    ClearCaptures();
                }
            });

            PromptForPermissionAndSendErrorEmailCommand = new Command(async () =>
            {
                var sendReport = await CoreMethods.DisplayAlert("Error",
                    "An error has occurred. Would you like to send an error report?", "Yes", "No");
                if (sendReport)
                {
                    Device.OpenUri(new Uri("mailto:me@kra2008.com?subject=CrossCam%20error%20report&body=" +
                                           ErrorMessage));
                }

                ErrorMessage = null;
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
            FirstImageSource = null;
            SecondImageSource = null;
            IsCameraVisible = true;

            if (Settings.IsTapToFocusEnabled)
            {
                SwitchToContinuousFocusTrigger = !SwitchToContinuousFocusTrigger;
            }
        }
    }
}