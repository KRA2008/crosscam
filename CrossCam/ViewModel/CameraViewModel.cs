using System;
using System.IO;
using System.Threading.Tasks;
using System.Web;
using CrossCam.Model;
using CrossCam.Page;
using CrossCam.Wrappers;
using FreshMvvm;
using SkiaSharp;
using Xamarin.Forms;

namespace CrossCam.ViewModel
{
    public sealed class CameraViewModel : FreshBasePageModel
    {
        public SKBitmap LeftBitmap { get; set; }
        public Command RetakeLeftCommand { get; set; }
        public bool LeftCaptureSuccess { get; set; }
        
        public SKBitmap RightBitmap { get; set; }
        public Command RetakeRightCommand { get; set; }
        public bool RightCaptureSuccess { get; set; }

        private bool _needs180Flip { get; set; }

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

        public Command EnterCropModeCommand { get; set; }
        public Command LeaveCropModeCommand { get; set; }
        public bool InCropMode { get; set; }

        public Command ClearCropsCommand { get; set; }

        public Command PromptForPermissionAndSendErrorEmailCommand { get; set; }
        public string ErrorMessage { get; set; }

        public Settings Settings { get; set; }

        private const int CROP_SPEED = 10;
        public Command IncreaseLLCrop => new Command(() => { LeftImageLeftCrop += CROP_SPEED; });
        public Command DecreaseLLCrop => new Command(() => { LeftImageLeftCrop -= LeftImageLeftCrop > 0 ? CROP_SPEED : 0; });
        public Command IncreaseLRCrop => new Command(() => { LeftImageRightCrop += CROP_SPEED; });
        public Command DecreaseLRCrop => new Command(() => { LeftImageRightCrop -= LeftImageRightCrop > 0 ? CROP_SPEED : 0; });
        public Command IncreaseRLCrop => new Command(() => { RightImageLeftCrop += CROP_SPEED; });
        public Command DecreaseRLCrop => new Command(() => { RightImageLeftCrop -= RightImageLeftCrop > 0 ? CROP_SPEED : 0; });
        public Command IncreaseRRCrop => new Command(() => { RightImageRightCrop += CROP_SPEED; });
        public Command DecreaseRRCrop => new Command(() => { RightImageRightCrop -= RightImageRightCrop > 0 ? CROP_SPEED : 0; });
        public Command IncreaseTopCrop => new Command(() => { TopCrop += CROP_SPEED; });
        public Command DecreaseTopCrop => new Command(() => { TopCrop -= TopCrop > 0 ? CROP_SPEED : 0; });
        public Command IncreaseBottomCrop => new Command(() => { BottomCrop += CROP_SPEED; });
        public Command DecreaseBottomCrop => new Command(() => { BottomCrop -= BottomCrop > 0 ? CROP_SPEED : 0; });

        public int LeftImageLeftCrop { get; set; }
        public int LeftImageRightCrop { get; set; }
        public int RightImageLeftCrop { get; set; }
        public int RightImageRightCrop { get; set; }
        public int TopCrop { get; set; }
        public int BottomCrop { get; set; }
        public int BorderThicknessOscillating => IsCaptureComplete ? BORDER_THICKNESS : 0;
        private const int BORDER_THICKNESS = CROP_SPEED * 6;

        public bool IsViewPortrait { get; set; }
        public bool IsCaptureLeftFirst { get; set; }
        public bool WasCapturePortrait { get; set; }

        public bool FailFadeTrigger { get; set; }
        public bool SuccessFadeTrigger { get; set; }
        public bool IsSaving { get; set; }

        public bool SwitchToContinuousFocusTrigger { get; set; }
        
        public bool IsCaptureComplete => LeftBitmap != null && RightBitmap != null;
        public bool IsNothingCaptured => LeftBitmap == null && RightBitmap == null;
        public bool ShouldCaptureButtonBeVisible => !IsCaptureComplete && !IsSaving && !IsViewMode && !InCropMode;
        public bool ShouldHelpTextBeVisible => IsNothingCaptured && !IsSaving && !IsViewMode && HelpTextColumn != CameraColumn && !InCropMode;
        public bool ShouldLeftRetakeBeVisible => LeftBitmap != null && !IsSaving && !IsViewMode && (!IsCaptureComplete || IsCaptureComplete && DoesCaptureOrientationMatchViewOrientation) && !InCropMode;
        public bool ShouldRightRetakeBeVisible => RightBitmap != null && !IsSaving && !IsViewMode && (!IsCaptureComplete || IsCaptureComplete && DoesCaptureOrientationMatchViewOrientation) && !InCropMode;
        public bool DoesCaptureOrientationMatchViewOrientation => WasCapturePortrait == IsViewPortrait;
        public bool ShouldEndButtonsBeVisible => IsCaptureComplete && !IsSaving && !IsViewMode && !InCropMode;
        public bool ShouldSettingsAndHelpBeVisible => !IsSaving && !IsViewMode;
        public bool ShouldLineGuidesBeVisible => (LeftBitmap == null ^ RightBitmap == null || Settings.ShowGuideLinesWithFirstCapture && !IsCaptureComplete) && Settings.AreGuideLinesVisible && !IsSaving && !IsViewMode && !InCropMode;
        public bool ShouldDonutGuideBeVisible => (LeftBitmap == null ^ RightBitmap == null || Settings.ShowGuideDonutWithFirstCapture && !IsCaptureComplete) && Settings.IsGuideDonutVisible && !IsSaving && !IsViewMode && !InCropMode;
        public bool ShouldCropButtonsBeVisible => InCropMode && !IsViewMode;

        public string HelpText => "1) Frame up your subject" +
                                  "\n2) Take the first picture (but finish reading these directions first)" +
                                  "\n3) Move " + SlideDirection + "" +
                                  "\n4) Start cross viewing with the preview that will have taken the place of these instructions" +
                                  "\n5) Guide lines will have appeared, align the second picture so the guide lines and the 3D image itself appear clear and sharp" +
                                  "\n6) Take the second picture when the desired level of 3D is achieved";
        public string SlideDirection => IsCaptureLeftFirst ? "LEFT" : "RIGHT";
        public int HelpTextColumn => IsCaptureLeftFirst ? 1 : 0;

        public ImageSource LeftReticleImage => ImageSource.FromFile("squareOuter");
        public ImageSource RightReticleImage => Settings.IsGuideDonutBothDonuts
            ? ImageSource.FromFile("squareOuter")
            : ImageSource.FromFile("squareInner");

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
                        LeftBitmap = GetBitmapAndCorrectOrientation(CapturedImageBytes);

                        if (RightBitmap == null)
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
                        RightBitmap = GetBitmapAndCorrectOrientation(CapturedImageBytes);

                        if (LeftBitmap == null)
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
                ClearCrops();
                CameraColumn = 0;
                IsCameraVisible = true;
                LeftBitmap = null;
                if (RightBitmap != null)
                {
                    MoveRightTrigger = !MoveRightTrigger;
                }
            });

            RetakeRightCommand = new Command(() =>
            {
                ClearCrops();
                CameraColumn = 1;
                IsCameraVisible = true;
                RightBitmap = null;
                if (LeftBitmap != null)
                {
                    MoveLeftTrigger = !MoveLeftTrigger;
                }
            });

            EnterCropModeCommand = new Command(() =>
            {
                InCropMode = true;
            });

            LeaveCropModeCommand = new Command(() =>
            {
                InCropMode = false;
            });

            ClearCropsCommand = new Command(ClearCrops);

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

                var tempArray = LeftBitmap;
                LeftBitmap = RightBitmap;
                RightBitmap = tempArray;

                if (IsCameraVisible)
                {
                    CameraColumn = CameraColumn == 0 ? 1 : 0;
                }

                if (LeftBitmap != null &&
                    RightBitmap == null)
                {
                    MoveLeftTrigger = !MoveLeftTrigger;
                }

                if (LeftBitmap == null &&
                    RightBitmap != null)
                {
                    MoveRightTrigger = !MoveRightTrigger;
                }

                Settings.IsCaptureLeftFirst = IsCaptureLeftFirst;
                PersistentStorage.Save(PersistentStorage.SETTINGS_KEY, Settings);
            });

            SaveCapturesCommand = new Command(async () =>
            {
                IsSaving = true;

                await Task.Delay(100); // take a break to go update the screen

                var leftBitmap = LeftBitmap;
                var rightBitmap = RightBitmap;
                SKImage leftSkImage = null;
                SKImage rightSkImage = null;
                SKImage finalImage = null;
                try
                {
                    LeftBitmap = null;
                    RightBitmap = null;
                    
                    double eachSideWidth;
                    if (leftBitmap.Height > leftBitmap.Width)
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

                    var didSave = true;

                    byte[] finalBytesToSave;
                    if (Settings.SaveSidesSeparately)
                    {
                        using (var tempSurface =
                            SKSurface.Create(new SKImageInfo((int) floatedWidth, leftBitmap.Height)))
                        {
                            var canvas = tempSurface.Canvas;

                            canvas.Clear(SKColors.Transparent);

                            canvas.DrawBitmap(leftBitmap,
                                SKRect.Create(floatedTrim, 0, floatedWidth, leftBitmap.Height),
                                SKRect.Create(0, 0, floatedWidth, leftBitmap.Height));

                            leftSkImage = tempSurface.Snapshot();

                            canvas.Clear(SKColors.Transparent);

                            canvas.DrawBitmap(rightBitmap,
                                SKRect.Create(floatedTrim, 0, floatedWidth, rightBitmap.Height),
                                SKRect.Create(0, 0, floatedWidth, rightBitmap.Height));

                            rightSkImage = tempSurface.Snapshot();

                        }

                        rightBitmap.Dispose();
                        leftBitmap.Dispose();

                        using (var encoded = leftSkImage.Encode(SKEncodedImageFormat.Jpeg, 100))
                        {
                            finalBytesToSave = encoded.ToArray();
                        }

                        didSave = await photoSaver.SavePhoto(finalBytesToSave);

                        using (var encoded = rightSkImage.Encode(SKEncodedImageFormat.Jpeg, 100))
                        {
                            finalBytesToSave = encoded.ToArray();
                        }

                        didSave = didSave && await photoSaver.SavePhoto(finalBytesToSave);
                    }
                    else
                    {
                        if (Settings.SaveRedundantFirstSide)
                        {
                            using (var tempSurface =
                                SKSurface.Create(new SKImageInfo(leftBitmap.Width, leftBitmap.Height)))
                            {
                                var canvas = tempSurface.Canvas;
                                canvas.Clear(SKColors.Transparent);

                                if (_needs180Flip)
                                {
                                    canvas.RotateDegrees(180);
                                    canvas.Translate(-1 * leftBitmap.Width, -1 * leftBitmap.Height);
                                }

                                canvas.DrawBitmap(IsCaptureLeftFirst ? leftBitmap : rightBitmap,
                                    SKRect.Create(floatedTrim, 0, floatedWidth, leftBitmap.Height),
                                    SKRect.Create(0, 0, floatedWidth, leftBitmap.Height));

                                finalImage = tempSurface.Snapshot();
                            }

                            using (var encoded = finalImage.Encode(SKEncodedImageFormat.Jpeg, 100))
                            {
                                finalBytesToSave = encoded.ToArray();
                            }

                            didSave = await photoSaver.SavePhoto(finalBytesToSave);
                        }

                        //cross view save

                        var finalImageWidth = leftBitmap.Width + rightBitmap.Width - LeftImageLeftCrop -
                                              LeftImageRightCrop - RightImageLeftCrop - RightImageRightCrop +
                                              4 * BORDER_THICKNESS;
                        var finalImageHeight = leftBitmap.Height - TopCrop - BottomCrop + 2 * BORDER_THICKNESS;

                        using (var tempSurface =
                            SKSurface.Create(new SKImageInfo(finalImageWidth, finalImageHeight)))
                        {
                            var canvas = tempSurface.Canvas;

                            canvas.Clear(SKColors.Black);

                            if (_needs180Flip)
                            {
                                canvas.RotateDegrees(180);
                                canvas.Translate(-1f * finalImageWidth, -1f * finalImageHeight);
                            }

                            DrawTool.DrawImagesOnCanvas(canvas, leftBitmap, rightBitmap, BORDER_THICKNESS, 
                                LeftImageLeftCrop, LeftImageRightCrop, RightImageLeftCrop, RightImageRightCrop,
                                TopCrop, BottomCrop);

                            finalImage = tempSurface.Snapshot();
                        }

                        if (!Settings.SaveForParallel)
                        {
                            leftBitmap.Dispose();
                            rightBitmap.Dispose();
                        }

                        using (var encoded = finalImage.Encode(SKEncodedImageFormat.Jpeg, 100))
                        {
                            finalBytesToSave = encoded.ToArray();
                        }

                        if (!Settings.SaveForParallel)
                        {
                            finalImage.Dispose();
                        }

                        var crossJoinedSave = await photoSaver.SavePhoto(finalBytesToSave);
                        didSave = didSave && crossJoinedSave;

                        if (Settings.SaveForParallel)
                        {
                            //TODO: consider extracting this to method but not sure if really cleaner
                            using (var tempSurface =
                                SKSurface.Create(new SKImageInfo((int)finalImageWidth, (int)finalImageHeight)))
                            {
                                var canvas = tempSurface.Canvas;

                                canvas.Clear(SKColors.Black);

                                DrawTool.DrawImagesOnCanvas(canvas, rightBitmap, leftBitmap, BorderThicknessOscillating,
                                    LeftImageLeftCrop, LeftImageRightCrop, RightImageLeftCrop, RightImageRightCrop,
                                    TopCrop, BottomCrop);

                                finalImage = tempSurface.Snapshot();
                            }
                            
                            leftBitmap.Dispose();
                            rightBitmap.Dispose();

                            using (var encoded = finalImage.Encode(SKEncodedImageFormat.Jpeg, 100))
                            {
                                finalBytesToSave = encoded.ToArray();
                            }

                            finalImage.Dispose();

                            var parallelJoinedSave = await photoSaver.SavePhoto(finalBytesToSave);
                            didSave = didSave && parallelJoinedSave;
                        }
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
                    rightSkImage?.Dispose();
                    leftSkImage?.Dispose();
                    finalImage?.Dispose();
                    leftBitmap.Dispose();
                    rightBitmap.Dispose();

                    ClearCaptures();
                }
            });

            PromptForPermissionAndSendErrorEmailCommand = new Command(async () =>
            {
                var sendReport = await CoreMethods.DisplayAlert("Error",
                    "An error has occurred. Would you like to send an error report?", "Yes", "No");
                if (sendReport)
                {
                    var appVersionProvider = DependencyService.Get<IAppVersionProvider>();
                    var errorMessage = appVersionProvider.GetAppVersion() + 
                                       "\n" + 
                                       ErrorMessage;
                    Device.OpenUri(new Uri("mailto:me@kra2008.com?subject=CrossCam%20error%20report&body=" +
                                           HttpUtility.UrlEncode(errorMessage)));
                }

                ErrorMessage = null;
            });
        }

        private SKBitmap GetBitmapAndCorrectOrientation(byte[] byteArray)
        {
            SKCodecOrigin origin;

            using (var stream = new MemoryStream(byteArray))
            using (var data = SKData.Create(stream))
            using (var codec = SKCodec.Create(data))
            {
                origin = codec.Origin;
            }

            switch (origin)
            {
                case SKCodecOrigin.BottomRight:
                    _needs180Flip = true;
                    return BitmapRotate180(SKBitmap.Decode(byteArray));
                case SKCodecOrigin.RightTop:
                    _needs180Flip = false;
                    return BitmapRotate90(SKBitmap.Decode(byteArray));
                default:
                    _needs180Flip = false;
                    return SKBitmap.Decode(byteArray);
            }
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
            RaisePropertyChanged(nameof(RightReticleImage));
        }

        private void ClearCrops()
        {
            LeftImageLeftCrop = 0;
            LeftImageRightCrop = 0;
            RightImageLeftCrop = 0;
            RightImageRightCrop = 0;
            TopCrop = 0;
            BottomCrop = 0;
        }

        private void ClearCaptures()
        {
            LeftBitmap = null;
            RightBitmap = null;
            IsCameraVisible = true;
            ClearCrops();

            if (Settings.IsTapToFocusEnabled)
            {
                SwitchToContinuousFocusTrigger = !SwitchToContinuousFocusTrigger;
            }
        }
    }
}