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
        public WorkflowStage WorkflowStage { get; set; }

        public SKBitmap LeftBitmap { get; set; }
        public Command RetakeLeftCommand { get; set; }
        public bool LeftCaptureSuccess { get; set; }
        
        public SKBitmap RightBitmap { get; set; }
        public Command RetakeRightCommand { get; set; }
        public bool RightCaptureSuccess { get; set; }

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

        public Command ClearCapturesCommand { get; set; }

        public Command NavigateToSettingsCommand { get; set; }
        public Command NavigateToHelpCommand { get; set; }

        public Command SwapSidesCommand { get; set; }

        public Command GoToModeCommand { get; set; }
        public Command SaveEditCommand { get; set; }

        public Command ClearEditCommand { get; set; }

        public Command PromptForPermissionAndSendErrorEmailCommand { get; set; }
        public string ErrorMessage { get; set; }

        public Settings Settings { get; set; }
        
        public Command IncreaseLLCrop => new Command(() =>
        {
            LeftImageLeftCrop += Settings.CropSpeed;
            if (Settings.LockSideCroppingTogether)
            {
                RightImageRightCrop = LeftImageLeftCrop;
            }
        });
        public Command DecreaseLLCrop => new Command(() =>
        {
            LeftImageLeftCrop -= LeftImageLeftCrop > 0 ? Settings.CropSpeed : 0;
            if (Settings.LockSideCroppingTogether)
            {
                RightImageRightCrop = LeftImageLeftCrop;
            }
        });
        public Command IncreaseLRCrop => new Command(() =>
        {
            LeftImageRightCrop += Settings.CropSpeed;
            if (Settings.LockSideCroppingTogether)
            {
                RightImageLeftCrop = LeftImageRightCrop;
            }
        });
        public Command DecreaseLRCrop => new Command(() =>
        {
            LeftImageRightCrop -= LeftImageRightCrop > 0 ? Settings.CropSpeed : 0;
            if (Settings.LockSideCroppingTogether)
            {
                RightImageLeftCrop = LeftImageRightCrop;
            }
        });
        public Command IncreaseRLCrop => new Command(() =>
        {
            RightImageLeftCrop += Settings.CropSpeed;
            if (Settings.LockSideCroppingTogether)
            {
                LeftImageRightCrop = RightImageLeftCrop;
            }
        });
        public Command DecreaseRLCrop => new Command(() =>
        {
            RightImageLeftCrop -= RightImageLeftCrop > 0 ? Settings.CropSpeed : 0;
            if (Settings.LockSideCroppingTogether)
            {
                LeftImageRightCrop = RightImageLeftCrop;
            }
        });
        public Command IncreaseRRCrop => new Command(() =>
        {
            RightImageRightCrop += Settings.CropSpeed;
            if (Settings.LockSideCroppingTogether)
            {
                LeftImageLeftCrop = RightImageRightCrop;
            }
        });
        public Command DecreaseRRCrop => new Command(() =>
        {
            RightImageRightCrop -= RightImageRightCrop > 0 ? Settings.CropSpeed : 0;
            if (Settings.LockSideCroppingTogether)
            {
                LeftImageLeftCrop = RightImageRightCrop;
            }
        });
        public Command IncreaseTopCrop => new Command(() => { TopCrop += Settings.CropSpeed; });
        public Command DecreaseTopCrop => new Command(() => { TopCrop -= TopCrop > 0 ? Settings.CropSpeed : 0; });
        public Command IncreaseBottomCrop => new Command(() => { BottomCrop += Settings.CropSpeed; });
        public Command DecreaseBottomCrop => new Command(() => { BottomCrop -= BottomCrop > 0 ? Settings.CropSpeed : 0; });

        public int LeftImageLeftCrop { get; set; }
        public int LeftImageRightCrop { get; set; }
        public int RightImageLeftCrop { get; set; }
        public int RightImageRightCrop { get; set; }
        public int TopCrop { get; set; }
        public int BottomCrop { get; set; }

        public float RotationSpeed => 0.1f;

        public float LeftRotation { get; set; }
        public float RightRotation { get; set; }

        public Command IncreaseLeftRotation => new Command(() => { LeftRotation += RotationSpeed; });
        public Command DecreaseLeftRotation => new Command(() => { LeftRotation -= RotationSpeed; });
        public Command IncreaseRightRotation => new Command(() => { RightRotation += RotationSpeed; });
        public Command DecreaseRightRotation => new Command(() => { RightRotation -= RotationSpeed; });

        public bool IsViewPortrait { get; set; }
        public bool IsCaptureLeftFirst { get; set; }
        public bool WasCapturePortrait { get; set; }

        public bool FailFadeTrigger { get; set; }
        public bool SuccessFadeTrigger { get; set; }

        public bool SwitchToContinuousFocusTrigger { get; set; }
        
        public bool IsNothingCaptured => LeftBitmap == null && RightBitmap == null;
        public bool ShouldHelpTextBeVisible => IsNothingCaptured && HelpTextColumn != CameraColumn && WorkflowStage == WorkflowStage.Capture;
        public bool ShouldLeftRetakeBeVisible => LeftBitmap != null && (WorkflowStage == WorkflowStage.Capture || WorkflowStage == WorkflowStage.Final && DoesCaptureOrientationMatchViewOrientation);
        public bool ShouldRightRetakeBeVisible => RightBitmap != null && (WorkflowStage == WorkflowStage.Capture || WorkflowStage == WorkflowStage.Final && DoesCaptureOrientationMatchViewOrientation);
        public bool DoesCaptureOrientationMatchViewOrientation => WasCapturePortrait == IsViewPortrait;
        public bool ShouldSettingsAndHelpBeVisible => WorkflowStage != WorkflowStage.Saving && WorkflowStage != WorkflowStage.View;
        public bool ShouldLineGuidesBeVisible => (LeftBitmap == null ^ RightBitmap == null || Settings.ShowGuideLinesWithFirstCapture && WorkflowStage == WorkflowStage.Capture) && Settings.AreGuideLinesVisible && WorkflowStage == WorkflowStage.Capture;
        public bool ShouldDonutGuideBeVisible => (LeftBitmap == null ^ RightBitmap == null || Settings.ShowGuideDonutWithFirstCapture && WorkflowStage == WorkflowStage.Capture) && Settings.IsGuideDonutVisible && WorkflowStage == WorkflowStage.Capture;
        public bool ShouldLeftCropButtonsBeVisible => WorkflowStage == WorkflowStage.Crop && !Settings.LockSideCroppingTogether;
        public bool ShouldSaveEditsButtonBeVisible => WorkflowStage == WorkflowStage.Edits ||
                                                      WorkflowStage == WorkflowStage.Crop ||
                                                      WorkflowStage == WorkflowStage.Rotate ||
                                                      WorkflowStage == WorkflowStage.Pan;
        public bool ShouldClearAndViewEditsButtonsBeVisible => WorkflowStage == WorkflowStage.Crop ||
                                                               WorkflowStage == WorkflowStage.Rotate ||
                                                               WorkflowStage == WorkflowStage.Pan;

        public string HelpText => "(flip for " + OppositeOrientation + ")" +
                                  "\n1) Frame up your subject" +
                                  "\n2) Take the first picture (but finish reading these directions first)" +
                                  "\n3) Move " + SlideDirection + "" +
                                  "\n4) Start cross viewing with the preview that will have taken the place of these instructions" +
                                  "\n5) Guide lines will have appeared, align the second picture so the guide lines and the 3D image itself appear clear and sharp" +
                                  "\n6) Take the second picture when the desired level of 3D is achieved";
        public string SlideDirection => IsCaptureLeftFirst ? "LEFT" : "RIGHT";
        public int HelpTextColumn => IsCaptureLeftFirst ? 1 : 0;
        public string OppositeOrientation => IsViewPortrait ? "landscape" : "portrait";
        public bool ShouldPortraitCaptureLandscapeViewModeWarningBeVisible => WorkflowStage != WorkflowStage.Capture && WorkflowStage != WorkflowStage.Saving && IsViewPortrait && WasCapturePortrait;

        public ImageSource LeftReticleImage => ImageSource.FromFile("squareOuter");
        public ImageSource RightReticleImage => Settings.IsGuideDonutBothDonuts
            ? ImageSource.FromFile("squareOuter")
            : ImageSource.FromFile("squareInner");

        private bool _needs180Flip;
        private WorkflowStage _stageBeforeView;

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
                            WorkflowStage = WorkflowStage.Final;
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
                            WorkflowStage = WorkflowStage.Final;
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
                WorkflowStage = WorkflowStage.Capture;
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
                WorkflowStage = WorkflowStage.Capture;
            });

            GoToModeCommand = new Command<WorkflowStage>(arg =>
            {
                WorkflowStage = arg;
            });

            SaveEditCommand = new Command(() =>
            {
                WorkflowStage = WorkflowStage == WorkflowStage.Edits ? WorkflowStage.Final : WorkflowStage.Edits;
            });

            ClearEditCommand = new Command(() =>
            {
                switch (WorkflowStage)
                {
                    case WorkflowStage.Crop:
                        ClearCrops();
                        break;
                    case WorkflowStage.Rotate:
                        ClearRotation();
                        break;
                    case WorkflowStage.Pan:
                        break;
                }
            });

            ClearCapturesCommand = new Command(ClearCaptures);

            CapturePictureCommand = new Command(() =>
            {
                CapturePictureTrigger = !CapturePictureTrigger;
            });

            ToggleViewModeCommand = new Command(() =>
            {
                if (WorkflowStage != WorkflowStage.View)
                {
                    _stageBeforeView = WorkflowStage;
                    WorkflowStage = WorkflowStage.View;
                }
                else
                {
                    WorkflowStage = _stageBeforeView;
                }
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
                WorkflowStage = WorkflowStage.Saving;

                var leftBitmap = LeftBitmap;
                var rightBitmap = RightBitmap;

                SKImage leftSkImage = null;
                SKImage rightSkImage = null;
                SKImage finalImage = null;
                try
                {
                    LeftBitmap = null;
                    RightBitmap = null;

                    await Task.Delay(100); // take a break to go update the screen

                    var didSave = true;

                    byte[] finalBytesToSave;

                    if (Settings.SaveSidesSeparately)
                    {
                        using (var tempSurface =
                            SKSurface.Create(new SKImageInfo(leftBitmap.Width, leftBitmap.Height)))
                        {
                            var canvas = tempSurface.Canvas;

                            canvas.Clear();
                            if (_needs180Flip)
                            {
                                canvas.RotateDegrees(180);
                                canvas.Translate(-1f * leftBitmap.Width, -1f * leftBitmap.Height);
                            }
                            canvas.DrawBitmap(leftBitmap, 0, 0);
                            leftSkImage = tempSurface.Snapshot();

                            canvas.Clear();
                            canvas.DrawBitmap(rightBitmap, 0, 0);
                            rightSkImage = tempSurface.Snapshot();
                        }

                        using (var encoded = leftSkImage.Encode(SKEncodedImageFormat.Jpeg, 100))
                        {
                            finalBytesToSave = encoded.ToArray();
                        }

                        // ReSharper disable once ConditionIsAlwaysTrueOrFalse - just let it go
                        didSave = didSave && await photoSaver.SavePhoto(finalBytesToSave);

                        using (var encoded = rightSkImage.Encode(SKEncodedImageFormat.Jpeg, 100))
                        {
                            finalBytesToSave = encoded.ToArray();
                        }

                        didSave = didSave && await photoSaver.SavePhoto(finalBytesToSave);
                    }

                    if (Settings.SaveRedundantFirstSide)
                    {
                        using (var tempSurface =
                            SKSurface.Create(new SKImageInfo(leftBitmap.Width, leftBitmap.Height)))
                        {
                            var canvas = tempSurface.Canvas;
                            canvas.Clear();
                            if (_needs180Flip)
                            {
                                canvas.RotateDegrees(180);
                                canvas.Translate(-1f * leftBitmap.Width, -1f * leftBitmap.Height);
                            }
                            canvas.DrawBitmap(IsCaptureLeftFirst ? leftBitmap : rightBitmap, 0, 0);

                            finalImage = tempSurface.Snapshot();
                        }

                        using (var encoded = finalImage.Encode(SKEncodedImageFormat.Jpeg, 100))
                        {
                            finalBytesToSave = encoded.ToArray();
                        }

                        didSave = didSave && await photoSaver.SavePhoto(finalBytesToSave);
                    }

                    var finalImageWidth = leftBitmap.Width + rightBitmap.Width - LeftImageLeftCrop -
                                          LeftImageRightCrop - RightImageLeftCrop - RightImageRightCrop +
                                          4 * (Settings.AddBorder ? Settings.BorderThickness : 0);
                    var finalImageHeight = leftBitmap.Height - TopCrop - BottomCrop +
                                           2 * (Settings.AddBorder ? Settings.BorderThickness : 0);

                    if (Settings.SaveForCrossView)
                    {
                        using (var tempSurface =
                            SKSurface.Create(new SKImageInfo(finalImageWidth, finalImageHeight)))
                        {
                            var canvas = tempSurface.Canvas;
                            canvas.Clear();
                            if (_needs180Flip)
                            {
                                canvas.RotateDegrees(180);
                                canvas.Translate(-1f * finalImageWidth, -1f * finalImageHeight);
                            }
                            DrawTool.DrawImagesOnCanvas(canvas, leftBitmap, rightBitmap, Settings.AddBorder ? Settings.BorderThickness : 0,
                                LeftImageLeftCrop, LeftImageRightCrop, RightImageLeftCrop, RightImageRightCrop,
                                TopCrop, BottomCrop, LeftRotation, RightRotation);

                            finalImage = tempSurface.Snapshot();
                        }

                        using (var encoded = finalImage.Encode(SKEncodedImageFormat.Jpeg, 100))
                        {
                            finalBytesToSave = encoded.ToArray();
                        }
                        
                        didSave = didSave && await photoSaver.SavePhoto(finalBytesToSave);
                    }

                    if (Settings.SaveForParallel)
                    {
                        using (var tempSurface =
                            SKSurface.Create(new SKImageInfo(finalImageWidth, finalImageHeight)))
                        {
                            var canvas = tempSurface.Canvas;
                            canvas.Clear();
                            if (_needs180Flip)
                            {
                                canvas.RotateDegrees(180);
                                canvas.Translate(-1f * finalImageWidth, -1f * finalImageHeight);
                            }
                            DrawTool.DrawImagesOnCanvas(canvas, leftBitmap, rightBitmap, Settings.AddBorder ? Settings.BorderThickness : 0,
                                LeftImageLeftCrop, LeftImageRightCrop, RightImageLeftCrop, RightImageRightCrop,
                                TopCrop, BottomCrop, LeftRotation, RightRotation, true);

                            finalImage = tempSurface.Snapshot();
                        }

                        using (var encoded = finalImage.Encode(SKEncodedImageFormat.Jpeg, 100))
                        {
                            finalBytesToSave = encoded.ToArray();
                        }
                        
                        didSave = didSave && await photoSaver.SavePhoto(finalBytesToSave);
                    }

                    WorkflowStage = WorkflowStage.Capture;

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

                    WorkflowStage = WorkflowStage.Capture;

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
            RaisePropertyChanged(nameof(ShouldLeftCropButtonsBeVisible));
            RaisePropertyChanged(nameof(Settings)); // this doesn't cause reevaluation for above stuff, but triggers redraw of canvas
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

        private void ClearRotation()
        {
            LeftRotation = 0;
            RightRotation = 0;
        }

        private void ClearCaptures()
        {
            LeftBitmap = null;
            RightBitmap = null;
            IsCameraVisible = true;
            ClearCrops();
            ClearRotation();
            WorkflowStage = WorkflowStage.Capture;

            if (Settings.IsTapToFocusEnabled)
            {
                SwitchToContinuousFocusTrigger = !SwitchToContinuousFocusTrigger;
            }
        }
    }
}