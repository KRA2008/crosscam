using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using CrossCam.Model;
using CrossCam.Page;
using CrossCam.Wrappers;
using FreshMvvm;
using Plugin.DeviceInfo;
using SkiaSharp;
using Xamarin.Forms;

namespace CrossCam.ViewModel
{
    public sealed class CameraViewModel : FreshBasePageModel
    {
        public WorkflowStage WorkflowStage { get; set; }
        public CropMode CropMode { get; set; }
        public ManualAlignMode ManualAlignMode { get; set; }
        public KeystoneMode KeystoneMode { get; set; }

        public SKBitmap LeftBitmap { get; set; }
        public Command RetakeLeftCommand { get; set; }
        public bool LeftCaptureSuccess { get; set; }
        
        public SKBitmap RightBitmap { get; set; }
        public Command RetakeRightCommand { get; set; }
        public bool RightCaptureSuccess { get; set; }

        private SKBitmap _replacedAutoAlignedBitmap { get; set; }

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

        public Command SetManualAlignMode { get; set; }

        public int ZoomMax { get; set; }
        public int LeftZoom { get; set; }
        public int RightZoom { get; set; }

        public int LeftCrop { get; set; }
        public int RightCrop { get; set; }
        public int InsideCrop { get; set; }
        public int OutsideCrop { get; set; }
        public int TopCrop { get; set; }
        public int BottomCrop { get; set; }

        public int SideCropMax { get; set; }
        public int TopOrBottomCropMax { get; set; }

        public Command SetCropMode { get; set; }

        public int VerticalAlignmentMax { get; set; }
        public int VerticalAlignment { get; set; }
        public int VerticalAlignmentMin => -VerticalAlignmentMax;

        public float RotationMax => 10;
        public float LeftRotation { get; set; }
        public float RightRotation { get; set; }
        public float RotationMin => -RotationMax;

        public Command SetKeystoneMode { get; set; }

        public float MaxKeystone => 0.25f;
        public float LeftKeystone { get; set; }
        public float RightKeystone { get; set; }

        public Command LoadPhotoCommand { get; set; }

        public bool IsViewPortrait { get; set; }
        public bool IsViewInverted { get; set; }
        public bool IsCaptureLeftFirst { get; set; }
        public bool WasCapturePortrait { get; set; }

        public bool AutomaticAlignmentNotSupportedTrigger { get; set; }
        public bool AlignmentFailFadeTrigger { get; set; }
        public bool SaveFailFadeTrigger { get; set; }
        public bool SaveSuccessFadeTrigger { get; set; }

        public bool SwitchToContinuousFocusTrigger { get; set; }

        public bool IsNothingCaptured => LeftBitmap == null && RightBitmap == null;
        public bool ShouldIconBeVisible => IsNothingCaptured && IconColumn != CameraColumn && WorkflowStage == WorkflowStage.Capture;
        public bool ShouldLeftRetakeBeVisible => LeftBitmap != null && (WorkflowStage == WorkflowStage.Capture || WorkflowStage == WorkflowStage.Final && DoesCaptureOrientationMatchViewOrientation);
        public bool ShouldRightLeftRetakeBeVisible => RightBitmap != null && WorkflowStage == WorkflowStage.Capture;
        public bool ShouldRightRightRetakeBeVisible => RightBitmap != null && WorkflowStage == WorkflowStage.Final && DoesCaptureOrientationMatchViewOrientation;
        public bool DoesCaptureOrientationMatchViewOrientation => WasCapturePortrait == IsViewPortrait;
        public bool ShouldSettingsAndHelpBeVisible => !IsBusy && 
                                                      WorkflowStage != WorkflowStage.View;
        public bool IsExactlyOnePictureTaken => LeftBitmap == null ^ RightBitmap == null;
        public bool ShouldLineGuidesBeVisible => (IsExactlyOnePictureTaken || Settings.ShowGuideLinesWithFirstCapture && WorkflowStage == WorkflowStage.Capture) && Settings.AreGuideLinesVisible || WorkflowStage == WorkflowStage.Keystone || WorkflowStage == WorkflowStage.ManualAlign;
        public bool ShouldDonutGuideBeVisible => (IsExactlyOnePictureTaken || Settings.ShowGuideDonutWithFirstCapture && WorkflowStage == WorkflowStage.Capture) && Settings.IsGuideDonutVisible;
        public bool ShouldRollGuideBeVisible => WorkflowStage == WorkflowStage.Capture && Settings.ShowRollGuide;
        public bool ShouldPitchGuideBeVisible => IsExactlyOnePictureTaken && Settings.ShowPitchGuide;
        public bool ShouldYawGuideBeVisible => IsExactlyOnePictureTaken && Settings.ShowYawGuide;
        public bool ShouldViewButtonBeVisible => WorkflowStage == WorkflowStage.Final ||
                                                 WorkflowStage == WorkflowStage.Crop ||
                                                 WorkflowStage == WorkflowStage.Keystone ||
                                                 WorkflowStage == WorkflowStage.ManualAlign;
        public bool ShouldClearEditButtonBeVisible => WorkflowStage == WorkflowStage.Crop ||
                                                      WorkflowStage == WorkflowStage.Keystone ||
                                                      WorkflowStage == WorkflowStage.ManualAlign;
        public bool IsBusy => WorkflowStage == WorkflowStage.Loading ||
                              WorkflowStage == WorkflowStage.AutomaticAlign ||
                              WorkflowStage == WorkflowStage.Saving;
        public bool ShouldSaveCapturesButtonBeVisible => WorkflowStage == WorkflowStage.Final &&
                                                         (Settings.SaveForCrossView ||
                                                          Settings.SaveForParallel ||
                                                          Settings.SaveSidesSeparately ||
                                                          Settings.SaveRedundantFirstSide);
        
        public int IconColumn => IsCaptureLeftFirst ? 1 : 0;

        public bool ShouldPortraitViewModeWarningBeVisible => IsViewPortrait &&
                                                              WorkflowStage != WorkflowStage.Saving &&
                                                              (IsNothingCaptured ||
                                                               WorkflowStage == WorkflowStage.Final ||
                                                               WorkflowStage == WorkflowStage.Edits);
        public string PortraitToLandscapeHint =>
            WorkflowStage == WorkflowStage.Capture ? "(flip for landscape)" : WorkflowStage == WorkflowStage.Edits ? "(flip to landscape for easier editing)" : "(flip to landscape for a better view)";

        public ImageSource LeftReticleImage => ImageSource.FromFile("squareOuter");
        public ImageSource RightReticleImage => Settings.IsGuideDonutBothDonuts
            ? ImageSource.FromFile("squareOuter")
            : ImageSource.FromFile("squareInner");
        
        private WorkflowStage _stageBeforeView;
        private int _wasAutomaticAlignmentRun;

        public CameraViewModel()
        {
            var photoSaver = DependencyService.Get<IPhotoSaver>();
            IsCameraVisible = true;

            Settings = PersistentStorage.LoadOrDefault(PersistentStorage.SETTINGS_KEY, new Settings());

            IsCaptureLeftFirst = Settings.IsCaptureLeftFirst;
            CameraColumn = IsCaptureLeftFirst ? 0 : 1;

            SideCropMax = 1;
            TopOrBottomCropMax = 1;
            VerticalAlignmentMax = 1;
            ZoomMax = 1;
            
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
                        LeftBytesCaptured(CapturedImageBytes);
                    }
                    else
                    {
                        RightBytesCaptured(CapturedImageBytes);
                    }
                }
                else if (args.PropertyName == nameof(ErrorMessage))
                {
                    if (ErrorMessage != null)
                    {
                        PromptForPermissionAndSendErrorEmailCommand.Execute(null);
                    }
                }
                else if (args.PropertyName == nameof(Settings))
                {
                    if (Settings.IsAutomaticAlignmentOn &&
                        RightBitmap != null &&
                        LeftBitmap != null)
                    {
                        AutoAlignIfNotYetRun();
                    }

                    if (!Settings.IsAutomaticAlignmentOn &&
                        _wasAutomaticAlignmentRun == 1)
                    {
                        ClearCrops();
                        if (IsCaptureLeftFirst)
                        {
                            SetRightBitmap(_replacedAutoAlignedBitmap);
                        }
                        else
                        {
                            SetLeftBitmap(_replacedAutoAlignedBitmap);
                        }

                        _wasAutomaticAlignmentRun = 0;
                    }
                }
            };

            LoadPhotoCommand = new Command(async () =>
            {
                const string FULL_IMAGE = "Load full stereo image";
                const string SINGLE_SIDE = "Load single side";
                const string CANCEL = "Cancel";
                var loadType = await CoreMethods.DisplayActionSheet("Choose an action:", CANCEL, null,
                    FULL_IMAGE, SINGLE_SIDE);

                if (loadType == CANCEL) return;

                var photo = await DependencyService.Get<IPhotoPicker>().GetImage();

                if (photo != null)
                {
                    if (loadType == FULL_IMAGE)
                    {
                        WorkflowStage = WorkflowStage.Loading;
                        var leftHalf = await Task.Run(() => GetHalfOfFullStereoImage(photo, true));
                        SetLeftBitmap(leftHalf, false);
                        var rightHalf = await Task.Run(() => GetHalfOfFullStereoImage(photo, false));
                        SetRightBitmap(rightHalf, false);
                    }
                    else if (loadType == SINGLE_SIDE)
                    {
                        CapturedImageBytes = photo;
                    }
                }
            });

            RetakeLeftCommand = new Command(() =>
            {
                ClearEdits();
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
                ClearEdits();
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
                    case WorkflowStage.ManualAlign:
                        ClearAlignments();
                        break;
                    case WorkflowStage.Keystone:
                        ClearKeystone();
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

            SetCropMode = new Command(mode =>
            {
                CropMode = (CropMode) mode;
            });

            SetManualAlignMode = new Command(mode =>
            {
                ManualAlignMode = (ManualAlignMode) mode;
            });

            SetKeystoneMode = new Command(mode =>
            {
                KeystoneMode = (KeystoneMode) mode;
            });

            SaveCapturesCommand = new Command(async () =>
            {
                WorkflowStage = WorkflowStage.Saving;
                
                try
                {
                    await Task.Run(async () =>
                    {
                        var needs180Flip = Device.RuntimePlatform == Device.iOS && IsViewInverted;

                        if (Settings.SaveSidesSeparately)
                        {
                            using (var tempSurface =
                                SKSurface.Create(new SKImageInfo(LeftBitmap.Width, LeftBitmap.Height)))
                            {
                                var canvas = tempSurface.Canvas;

                                canvas.Clear();
                                if (needs180Flip)
                                {
                                    canvas.RotateDegrees(180);
                                    canvas.Translate(-1f * LeftBitmap.Width, -1f * LeftBitmap.Height);
                                }

                                canvas.DrawBitmap(LeftBitmap, 0, 0);
                                using (var leftSkImage = tempSurface.Snapshot())
                                {
                                    using (var encoded = leftSkImage.Encode(SKEncodedImageFormat.Jpeg, 100))
                                    {
                                        await photoSaver.SavePhoto(encoded.ToArray());
                                    }
                                }

                                canvas.Clear();
                                canvas.DrawBitmap(RightBitmap, 0, 0);
                                using (var rightSkImage = tempSurface.Snapshot())
                                {
                                    using (var encoded = rightSkImage.Encode(SKEncodedImageFormat.Jpeg, 100))
                                    {
                                        await photoSaver.SavePhoto(encoded.ToArray());
                                    }
                                }
                            }
                        }

                        if (Settings.SaveRedundantFirstSide)
                        {
                            using (var tempSurface =
                                SKSurface.Create(new SKImageInfo(LeftBitmap.Width, LeftBitmap.Height)))
                            {
                                var canvas = tempSurface.Canvas;
                                canvas.Clear();
                                if (needs180Flip)
                                {
                                    canvas.RotateDegrees(180);
                                    canvas.Translate(-1f * LeftBitmap.Width, -1f * LeftBitmap.Height);
                                }

                                canvas.DrawBitmap(IsCaptureLeftFirst ? LeftBitmap : RightBitmap, 0, 0);

                                using (var encoded = tempSurface.Snapshot().Encode(SKEncodedImageFormat.Jpeg, 100))
                                {
                                    await photoSaver.SavePhoto(encoded.ToArray());
                                }
                            }
                        }

                        var finalImageWidth = DrawTool.CalculateCanvasWidthLessBorder(LeftBitmap, RightBitmap,
                            LeftCrop + OutsideCrop, InsideCrop + RightCrop, InsideCrop + LeftCrop, RightCrop + OutsideCrop);
                        var borderThickness = Settings.AddBorder
                            ? (int) (DrawTool.BORDER_CONVERSION_FACTOR * Settings.BorderWidthProportion *
                                     finalImageWidth)
                            : 0;
                        finalImageWidth += 4 * borderThickness;
                        var finalImageHeight = DrawTool.CalculateCanvasHeightLessBorder(LeftBitmap, RightBitmap,
                                                   TopCrop, BottomCrop,
                                                   VerticalAlignment) +
                                               2 * borderThickness;

                        finalImageWidth = (int) (finalImageWidth * (Settings.ResolutionProportion / 100d));
                        finalImageHeight = (int) (finalImageHeight * (Settings.ResolutionProportion / 100d));

                        if (Settings.SaveForCrossView)
                        {
                            using (var tempSurface =
                                SKSurface.Create(new SKImageInfo(finalImageWidth, finalImageHeight)))
                            {
                                var canvas = tempSurface.Canvas;
                                canvas.Clear();
                                if (needs180Flip)
                                {
                                    canvas.RotateDegrees(180);
                                    canvas.Translate(-1f * finalImageWidth, -1f * finalImageHeight);
                                }

                                DrawTool.DrawImagesOnCanvas(canvas, LeftBitmap, RightBitmap,
                                    Settings.BorderWidthProportion, Settings.AddBorder, Settings.BorderColor,
                                    LeftCrop + OutsideCrop, InsideCrop + RightCrop, InsideCrop + LeftCrop, RightCrop + OutsideCrop, 
                                    TopCrop, BottomCrop,
                                    LeftRotation, RightRotation,
                                    VerticalAlignment,
                                    LeftZoom, RightZoom,
                                    LeftKeystone, RightKeystone);
                                
                                using (var encoded = tempSurface.Snapshot().Encode(SKEncodedImageFormat.Jpeg, 100))
                                {
                                    await photoSaver.SavePhoto(encoded.ToArray());
                                }
                            }
                        }

                        if (Settings.SaveForParallel)
                        {
                            using (var tempSurface =
                                SKSurface.Create(new SKImageInfo(finalImageWidth, finalImageHeight)))
                            {
                                var canvas = tempSurface.Canvas;
                                canvas.Clear();
                                if (needs180Flip)
                                {
                                    canvas.RotateDegrees(180);
                                    canvas.Translate(-1f * finalImageWidth, -1f * finalImageHeight);
                                }

                                DrawTool.DrawImagesOnCanvas(canvas, LeftBitmap, RightBitmap,
                                    Settings.BorderWidthProportion, Settings.AddBorder, Settings.BorderColor,
                                    LeftCrop + OutsideCrop, InsideCrop + RightCrop, InsideCrop + LeftCrop, RightCrop + OutsideCrop,
                                    TopCrop, BottomCrop,
                                    LeftRotation, RightRotation,
                                    VerticalAlignment,
                                    LeftZoom, RightZoom,
                                    LeftKeystone, RightKeystone,
                                    true);

                                using (var encoded = tempSurface.Snapshot().Encode(SKEncodedImageFormat.Jpeg, 100))
                                {
                                    await photoSaver.SavePhoto(encoded.ToArray());
                                }
                            }
                        }
                    });
                }
                catch (Exception e)
                {
                    SaveFailFadeTrigger = !SaveFailFadeTrigger;
                    ErrorMessage = e.ToString();
                    WorkflowStage = WorkflowStage.Final;

                    return;
                }
                
                SaveSuccessFadeTrigger = !SaveSuccessFadeTrigger;
                ClearCaptures();
            });

            PromptForPermissionAndSendErrorEmailCommand = new Command(async () =>
            {
                var sendReport = await CoreMethods.DisplayAlert("Error",
                    "An error has occurred. Would you like to send an error report?", "Yes", "No");
                if (sendReport)
                {
                    var errorMessage = ErrorMessage + "\n" +
                                       "\n" +
                                       "Device Platform: " + CrossDeviceInfo.Current.Platform + "\n" +
                                       "Device Manufacturer: " + CrossDeviceInfo.Current.Manufacturer + "\n" +
                                       "Device Model: " + CrossDeviceInfo.Current.Model + "\n" +
                                       "Device Width: " + Application.Current.MainPage.Width + "\n" +
                                       "Device Height: " + Application.Current.MainPage.Height + "\n" +
                                       "OS Version: " + CrossDeviceInfo.Current.Version + "\n" +
                                       "OS Version Number: " + CrossDeviceInfo.Current.VersionNumber + "\n" +
                                       "App Version: " + CrossDeviceInfo.Current.AppVersion + "\n" +
                                       "App Build: " + CrossDeviceInfo.Current.AppBuild + "\n" +
                                       "Idiom: " + CrossDeviceInfo.Current.Idiom;
                    Device.OpenUri(new Uri("mailto:me@kra2008.com?subject=CrossCam%20error%20report&body=" +
                                           HttpUtility.UrlEncode(errorMessage)));
                }

                ErrorMessage = null;
            });
        }

        private async void LeftBytesCaptured(byte[] capturedBytes)
        {
            var bitmap = await Task.Run(() => GetBitmapAndCorrectOrientation(capturedBytes));
            SetLeftBitmap(bitmap);
        }

        private async void RightBytesCaptured(byte[] capturedBytes)
        {
            var bitmap = await Task.Run(() => GetBitmapAndCorrectOrientation(capturedBytes));
            SetRightBitmap(bitmap);
        }

        private async void AutoAlignIfNotYetRun()
        {
            if (0 == Interlocked.Exchange(ref _wasAutomaticAlignmentRun, 1))
            {
                WorkflowStage = WorkflowStage.AutomaticAlign;

                var openCv = DependencyService.Get<IOpenCv>();

                AlignedResult alignedResult = null;
                if (openCv.IsOpenCvSupported())
                {
                    try
                    {
                        await Task.Run(() =>
                        {
                            alignedResult = openCv.CreateAlignedSecondImage(
                                IsCaptureLeftFirst ? LeftBitmap : RightBitmap,
                                IsCaptureLeftFirst ? RightBitmap : LeftBitmap,
                                Settings.AlignmentDownsizePercentage,
                                Settings.AlignmentIterations,
                                Settings.AlignmentEpsilonLevel,
                                Settings.AlignmentEccThresholdPercentage);
                        });
                    }
                    catch (Exception e)
                    {
                        ErrorMessage = e.ToString();
                    }

                    if (alignedResult != null)
                    {
                        var topLeft = alignedResult.TransformMatrix.MapPoint(0, 0);
                        var topRight = alignedResult.TransformMatrix.MapPoint(alignedResult.AlignedBitmap.Width - 1, 0);
                        var bottomRight = alignedResult.TransformMatrix.MapPoint(alignedResult.AlignedBitmap.Width - 1,
                            alignedResult.AlignedBitmap.Height - 1);
                        var bottomLeft =
                            alignedResult.TransformMatrix.MapPoint(0, alignedResult.AlignedBitmap.Height - 1);

                        if (topLeft.Y > topRight.Y)
                        {
                            if (topLeft.Y > 0)
                            {
                                TopCrop = (int) topLeft.Y;
                            }
                        }
                        else
                        {
                            if (topRight.Y > 0)
                            {
                                TopCrop = (int) topRight.Y;
                            }
                        }

                        var maxY = alignedResult.AlignedBitmap.Height - 1;
                        if (bottomLeft.Y < bottomRight.Y)
                        {
                            if (bottomLeft.Y < maxY)
                            {
                                BottomCrop = (int) (maxY - bottomLeft.Y);
                            }
                        }
                        else
                        {
                            if (bottomRight.Y < maxY)
                            {
                                BottomCrop = (int) (maxY - bottomRight.Y);
                            }
                        }

                        var alignedLeftCrop = 0;
                        if (topLeft.X > bottomLeft.X)
                        {
                            if (topLeft.X > 0)
                            {
                                alignedLeftCrop = (int) topLeft.X;
                            }
                        }
                        else
                        {
                            if (bottomLeft.X > 0)
                            {
                                alignedLeftCrop = (int) bottomLeft.X;
                            }
                        }

                        var alignedRightCrop = 0;
                        var maxX = alignedResult.AlignedBitmap.Width - 1;
                        if (topRight.X < bottomRight.X)
                        {
                            if (topRight.X < maxX)
                            {
                                alignedRightCrop = (int) (maxX - topRight.X);
                            }
                        }
                        else
                        {
                            if (bottomRight.X < maxX)
                            {
                                alignedRightCrop = (int) (maxX - bottomRight.X);
                            }
                        }

                        //this actually cuts off a bit more than it has to, but it is inconsequential for small deviations
                        //(it cuts at the corner of the original image, not at the point where the original border crosses the new border)

                        if (IsCaptureLeftFirst)
                        {
                            _replacedAutoAlignedBitmap = RightBitmap;
                            OutsideCrop = alignedRightCrop;
                            InsideCrop = alignedLeftCrop;
                            SetRightBitmap(alignedResult.AlignedBitmap);
                        }
                        else
                        {
                            _replacedAutoAlignedBitmap = LeftBitmap;
                            InsideCrop = alignedRightCrop;
                            OutsideCrop = alignedLeftCrop;
                            SetLeftBitmap(alignedResult.AlignedBitmap);
                        }
                    }
                    else
                    {
                        AlignmentFailFadeTrigger = !AlignmentFailFadeTrigger;
                    }
                }
                else
                {
                    AutomaticAlignmentNotSupportedTrigger = !AutomaticAlignmentNotSupportedTrigger;
                }

                WorkflowStage = WorkflowStage.Final;
            }
        }

        private void SetLeftBitmap(SKBitmap bitmap, bool withMovementTrigger = true)
        {
            LeftBitmap = bitmap;
            WasCapturePortrait = LeftBitmap.Width < LeftBitmap.Height;

            if (RightBitmap == null)
            {
                if (withMovementTrigger)
                {
                    MoveLeftTrigger = !MoveLeftTrigger;
                }
                CameraColumn = 1;
            }
            else
            {
                CameraColumn = IsCaptureLeftFirst ? 0 : 1;
                IsCameraVisible = false;
                WorkflowStage = WorkflowStage.Final;
                SetMaxEdits(bitmap);
                if (Settings.IsAutomaticAlignmentOn)
                {
                    AutoAlignIfNotYetRun();
                }
            }
        }

        private void SetRightBitmap(SKBitmap bitmap, bool withMovementTrigger = true)
        {
            RightBitmap = bitmap;
            WasCapturePortrait = RightBitmap.Width < RightBitmap.Height;

            if (LeftBitmap == null)
            {
                if (withMovementTrigger)
                {
                    MoveRightTrigger = !MoveRightTrigger;
                }
                CameraColumn = 0;
            }
            else
            {
                CameraColumn = IsCaptureLeftFirst ? 0 : 1;
                IsCameraVisible = false;
                WorkflowStage = WorkflowStage.Final;
                SetMaxEdits(bitmap);
                if (Settings.IsAutomaticAlignmentOn)
                {
                    AutoAlignIfNotYetRun();
                }
            }
        }

        private void SetMaxEdits(SKBitmap bitmap)
        {
            SideCropMax = bitmap.Width / 2;
            TopOrBottomCropMax = bitmap.Height / 2;
            VerticalAlignmentMax = bitmap.Height / 4;
            ZoomMax = bitmap.Height / 4;
        }

        private static SKBitmap GetHalfOfFullStereoImage(byte[] bytes, bool wantLeft)
        {
            var original = SKBitmap.Decode(bytes);

            const int BORDER_DIFF_THRESHOLD = 5;
            var bottomBorder = 0;
            var leftBorder = 0;
            var topBorder = 0;
            var rightBorder = 0;

            var topLeft = GetTotalColor(original.GetPixel(0, 0));
            var topRight = GetTotalColor(original.GetPixel(original.Width - 1, 0));
            var bottomRight = GetTotalColor(original.GetPixel(original.Width - 1, original.Height - 1));
            var bottomLeft = GetTotalColor(original.GetPixel(0, original.Height - 1));

            if (Math.Abs(topLeft - topRight) < BORDER_DIFF_THRESHOLD &&
                Math.Abs(topRight - bottomRight) < BORDER_DIFF_THRESHOLD &&
                Math.Abs(bottomRight - bottomLeft) < BORDER_DIFF_THRESHOLD &&
                Math.Abs(bottomLeft - topLeft) < BORDER_DIFF_THRESHOLD &&
                Math.Abs(topLeft - bottomRight) < BORDER_DIFF_THRESHOLD &&
                Math.Abs(topRight - bottomLeft) < BORDER_DIFF_THRESHOLD)
            {
                for (var ii = 0; ii < original.Width / 4; ii++)
                {
                    var color = original.GetPixel(ii, original.Height / 2);
                    if (Math.Abs(color.Red + color.Green + color.Blue - topLeft) > BORDER_DIFF_THRESHOLD)
                    {
                        leftBorder = ii;
                        break;
                    }
                }
                for (var ii = 0; ii < original.Height / 2; ii++)
                {
                    var color = original.GetPixel(original.Width / 4, ii);
                    if (Math.Abs(color.Red + color.Green + color.Blue - topLeft) > BORDER_DIFF_THRESHOLD)
                    {
                        topBorder = ii;
                        break;
                    }
                }
                for (var ii = original.Width / 2; ii > original.Width / 4; ii--)
                {
                    var color = original.GetPixel(ii, original.Height / 2);
                    if (Math.Abs(color.Red + color.Green + color.Blue - topLeft) > BORDER_DIFF_THRESHOLD)
                    {
                        rightBorder = original.Width / 2 - 1 - ii;
                        break;
                    }
                }
                for (var ii = original.Height - 1; ii > original.Height / 2; ii--)
                {
                    var color = original.GetPixel(original.Width / 4, ii);
                    if (Math.Abs(color.Red + color.Green + color.Blue - topLeft) > BORDER_DIFF_THRESHOLD)
                    {
                        bottomBorder = original.Height - ii;
                        break;
                    }
                }
            }

            var width = (int) Math.Round(original.Width / 2f) - leftBorder - rightBorder;
            var height = original.Height - topBorder - bottomBorder;

            var extracted = new SKBitmap(width, height);

            using (var surface = new SKCanvas(extracted))
            {
                surface.DrawBitmap(
                    original,
                    SKRect.Create(
                        wantLeft ? leftBorder : width + 2 * leftBorder + rightBorder,
                        topBorder,
                        width,
                        height),
                    SKRect.Create(
                        0,
                        0,
                        width,
                        height));
            }

            return extracted;
        }

        private static int GetTotalColor(SKColor color)
        {
            return color.Red + color.Green + color.Blue;
        }

        private static SKBitmap GetBitmapAndCorrectOrientation(byte[] bytes)
        {
            SKCodecOrigin origin;

            using (var stream = new MemoryStream(bytes))
            using (var data = SKData.Create(stream))
            using (var codec = SKCodec.Create(data))
            {
                origin = codec.Origin;
            }
            
            switch (origin)
            {
                case SKCodecOrigin.BottomRight:
                    return BitmapRotate180(SKBitmap.Decode(bytes));
                case SKCodecOrigin.RightTop:
                    return BitmapRotate90(SKBitmap.Decode(bytes));
                default:
                    return SKBitmap.Decode(bytes);
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

        protected override async void ViewIsAppearing(object sender, EventArgs e)
        {
            base.ViewIsAppearing(sender, e);
            RaisePropertyChanged(nameof(ShouldLineGuidesBeVisible)); //TODO: figure out how to have Fody do this
            RaisePropertyChanged(nameof(ShouldDonutGuideBeVisible));
            RaisePropertyChanged(nameof(ShouldRollGuideBeVisible));
            RaisePropertyChanged(nameof(ShouldPitchGuideBeVisible));
            RaisePropertyChanged(nameof(ShouldYawGuideBeVisible));
            RaisePropertyChanged(nameof(ShouldSaveCapturesButtonBeVisible));
            RaisePropertyChanged(nameof(RightReticleImage));
            RaisePropertyChanged(nameof(Settings)); // this doesn't cause reevaluation for above stuff (but I'd like it to), but it does trigger redraw of canvas and rerun of auto alignment

            await Task.Delay(100);
            await EvaluateAndShowWelcomePopup();
        }

        private async Task EvaluateAndShowWelcomePopup()
        {
            if (!Settings.HasOfferedTechniqueHelpBefore)
            {
                var showTechniquePage = await CoreMethods.DisplayAlert("Welcome to CrossCam!",
                    "CrossCam was made to help you take 3D photos. The photos are 3D just like VR or 3D movies are, but you don't need any special equipment or glasses - just your phone. The technique to view the 3D photos is a little tricky and takes some practice to get it right. Before I tell you how to use CrossCam, would you first like to learn more about the viewing technique?",
                    "Yes", "No");
                Settings.HasOfferedTechniqueHelpBefore = true;
                PersistentStorage.Save(PersistentStorage.SETTINGS_KEY, Settings);
                if (showTechniquePage)
                {
                    await CoreMethods.PushPageModel<TechniqueHelpViewModel>();
                }
                else
                {
                    await CoreMethods.PushPageModel<DirectionsViewModel>();
                    Settings.HasShownDirectionsBefore = true;
                    PersistentStorage.Save(PersistentStorage.SETTINGS_KEY, Settings);
                }
            }
            else
            {
                if (!Settings.HasShownDirectionsBefore)
                {
                    await CoreMethods.PushPageModel<DirectionsViewModel>();
                    Settings.HasShownDirectionsBefore = true;
                    PersistentStorage.Save(PersistentStorage.SETTINGS_KEY, Settings);
                }
            }
        }

        private void ClearCrops()
        {
            OutsideCrop = 0;
            InsideCrop = 0;
            RightCrop = 0;
            LeftCrop = 0;
            TopCrop = 0;
            BottomCrop = 0;
            CropMode = CropMode.Inside;
        }

        private void ClearAlignments()
        {
            LeftRotation = 0;
            RightRotation = 0;
            LeftZoom = 0;
            RightZoom = 0;
            VerticalAlignment = 0;
            ManualAlignMode = ManualAlignMode.VerticalAlign;
        }

        private void ClearKeystone()
        {
            LeftKeystone = 0;
            RightKeystone = 0;
            KeystoneMode = KeystoneMode.Left;
        }

        private void ClearEdits()
        {
            ClearCrops();
            ClearAlignments();
            ClearKeystone();
            _wasAutomaticAlignmentRun = 0;
        }

        private void ClearCaptures()
        {
            LeftBitmap?.Dispose();
            LeftBitmap = null;
            RightBitmap?.Dispose();
            RightBitmap = null;
            IsCameraVisible = true;
            ClearEdits();
            WorkflowStage = WorkflowStage.Capture;

            if (Settings.IsTapToFocusEnabled)
            {
                SwitchToContinuousFocusTrigger = !SwitchToContinuousFocusTrigger;
            }
        }
    }
}