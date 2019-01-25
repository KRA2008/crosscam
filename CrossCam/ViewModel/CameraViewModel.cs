using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
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

        public int LeftZoom { get; set; }
        public int RightZoom { get; set; }
        public Command LeftZoomIn => new Command(() =>
        {
            LeftZoom += Settings.ZoomSpeed;
        });
        public Command LeftZoomOut => new Command(() =>
        {
            if (LeftZoom - Settings.ZoomSpeed >= 0)
            {
                LeftZoom -= Settings.ZoomSpeed;
            }
        });
        public Command RightZoomIn => new Command(() =>
        {
            RightZoom += Settings.ZoomSpeed;
        });
        public Command RightZoomOut => new Command(() =>
        {
            if (RightZoom - Settings.ZoomSpeed >= 0)
            {
                RightZoom -= Settings.ZoomSpeed;
            }
        });

        public int LeftLeftCrop { get; set; }
        public int LeftRightCrop { get; set; }
        public int RightLeftCrop { get; set; }
        public int RightRightCrop { get; set; }
        public int LeftTopCrop { get; set; }
        public int LeftBottomCrop { get; set; }
        public int RightTopCrop { get; set; }
        public int RightBottomCrop { get; set; }

        public Command IncreaseInsideCrop => new Command(() =>
        {
            LeftRightCrop += Settings.CropSpeed;
            RightLeftCrop += Settings.CropSpeed;
        });
        public Command DecreaseInsideCrop => new Command(() =>
        {
            if (LeftRightCrop - Settings.CropSpeed >= 0 &&
                RightLeftCrop - Settings.CropSpeed >= 0)
            {
                LeftRightCrop -= Settings.CropSpeed;
                RightLeftCrop -= Settings.CropSpeed;
            }
        });
        public Command IncreaseOutsideCrop => new Command(() =>
        {
            LeftLeftCrop += Settings.CropSpeed;
            RightRightCrop += Settings.CropSpeed;
        });
        public Command DecreaseOutsideCrop => new Command(() =>
        {
            if (LeftLeftCrop - Settings.CropSpeed >= 0 &&
                RightRightCrop - Settings.CropSpeed >= 0)
            {
                LeftLeftCrop -= Settings.CropSpeed;
                RightRightCrop -= Settings.CropSpeed;
            }
        });

        public Command IncreaseLeftCrop => new Command(() =>
        {
            LeftLeftCrop += Settings.CropSpeed;
            RightLeftCrop += Settings.CropSpeed;
        });
        public Command DecreaseLeftCrop => new Command(() =>
        {
            if (LeftLeftCrop - Settings.CropSpeed >= 0 &&
                RightLeftCrop - Settings.CropSpeed >= 0)
            {
                LeftLeftCrop -= Settings.CropSpeed;
                RightLeftCrop -= Settings.CropSpeed;
            }
        });
        public Command IncreaseRightCrop => new Command(() =>
        {
            LeftRightCrop += Settings.CropSpeed;
            RightRightCrop += Settings.CropSpeed;
        });
        public Command DecreaseRightCrop => new Command(() =>
        {
            if (LeftRightCrop - Settings.CropSpeed >= 0 &&
                RightRightCrop - Settings.CropSpeed >= 0)
            {
                LeftRightCrop -= Settings.CropSpeed;
                RightRightCrop -= Settings.CropSpeed;
            }
        });

        public Command IncreaseTopCrop => new Command(() =>
        {
            LeftTopCrop += Settings.CropSpeed;
            RightTopCrop += Settings.CropSpeed;
        });
        public Command DecreaseTopCrop => new Command(() =>
        {
            if (LeftTopCrop - Settings.CropSpeed >= 0 &&
                RightTopCrop - Settings.CropSpeed > 0)
            {
                LeftTopCrop -= Settings.CropSpeed;
                RightTopCrop -= Settings.CropSpeed;
            }
        });
        public Command IncreaseBottomCrop => new Command(() =>
        {
            LeftBottomCrop += Settings.CropSpeed;
            RightBottomCrop += Settings.CropSpeed;
        });
        public Command DecreaseBottomCrop => new Command(() =>
        {
            if (LeftBottomCrop - Settings.CropSpeed >= 0 &&
                RightBottomCrop - Settings.CropSpeed > 0)
            {
                LeftBottomCrop -= Settings.CropSpeed;
                RightBottomCrop -= Settings.CropSpeed;
            }
        });
        
        public int ManualAlignment { get; set; }
        public Command LeftUpRightDown => new Command(() =>
        {
            ManualAlignment += Settings.AlignSpeed;
        });
        public Command LeftDownRightUp => new Command(() =>
        {
            ManualAlignment -= Settings.AlignSpeed;
        });

        private const float ROTATION_MULTIPLIER = 100f;

        public float LeftRotation { get; set; }
        public float RightRotation { get; set; }
        
        public Command IncreaseLeftRotation => new Command(() =>  LeftRotation += Settings.RotationSpeed / ROTATION_MULTIPLIER);
        public Command DecreaseLeftRotation => new Command(() => LeftRotation -= Settings.RotationSpeed / ROTATION_MULTIPLIER);
        public Command IncreaseRightRotation => new Command(() => RightRotation += Settings.RotationSpeed / ROTATION_MULTIPLIER);
        public Command DecreaseRightRotation => new Command(() => RightRotation -= Settings.RotationSpeed / ROTATION_MULTIPLIER);

        private const float KEYSTONE_MULTIPLIER = 10000f;

        public float LeftKeystone { get; set; }
        public float RightKeystone { get; set; }

        public Command IncreaseLeftKeystone => new Command(() => LeftKeystone += Settings.KeystoneSpeed / KEYSTONE_MULTIPLIER);
        public Command DecreaseLeftKeystone => new Command(() => LeftKeystone -= Settings.KeystoneSpeed / KEYSTONE_MULTIPLIER);
        public Command IncreaseRightKeystone => new Command(() => RightKeystone += Settings.KeystoneSpeed / KEYSTONE_MULTIPLIER);
        public Command DecreaseRightKeystone => new Command(() => RightKeystone -= Settings.KeystoneSpeed / KEYSTONE_MULTIPLIER);

        public Command LoadPhotoCommand { get; set; }

        public bool IsViewPortrait { get; set; }
        public bool IsViewInvertedLandscape { get; set; }
        public bool IsCaptureLeftFirst { get; set; }
        public bool WasCapturePortrait { get; set; }

        public bool AlignmentFailFadeTrigger { get; set; }
        public bool SaveFailFadeTrigger { get; set; }
        public bool SaveSuccessFadeTrigger { get; set; }

        public bool SwitchToContinuousFocusTrigger { get; set; }

        public bool ShouldLeftLoadBeVisible => LeftBitmap == null && CameraColumn == 0 && WorkflowStage == WorkflowStage.Capture;
        public bool ShouldRightLoadBeVisible => RightBitmap == null && CameraColumn == 1 && WorkflowStage == WorkflowStage.Capture;
        public bool IsNothingCaptured => LeftBitmap == null && RightBitmap == null;
        public bool ShouldIconBeVisible => IsNothingCaptured && IconColumn != CameraColumn && WorkflowStage == WorkflowStage.Capture;
        public bool ShouldLeftRetakeBeVisible => LeftBitmap != null && (WorkflowStage == WorkflowStage.Capture || WorkflowStage == WorkflowStage.Final && DoesCaptureOrientationMatchViewOrientation);
        public bool ShouldRightRetakeBeVisible => RightBitmap != null && (WorkflowStage == WorkflowStage.Capture || WorkflowStage == WorkflowStage.Final && DoesCaptureOrientationMatchViewOrientation);
        public bool DoesCaptureOrientationMatchViewOrientation => WasCapturePortrait == IsViewPortrait;
        public bool ShouldSettingsAndHelpBeVisible => !IsBusy && 
                                                      WorkflowStage != WorkflowStage.View;
        public bool IsExactlyOnePictureTaken => LeftBitmap == null ^ RightBitmap == null;
        public bool ShouldLineGuidesBeVisible => (IsExactlyOnePictureTaken || Settings.ShowGuideLinesWithFirstCapture && WorkflowStage == WorkflowStage.Capture) && Settings.AreGuideLinesVisible || WorkflowStage == WorkflowStage.Keystone || WorkflowStage == WorkflowStage.ManualAlign;
        public bool ShouldDonutGuideBeVisible => (IsExactlyOnePictureTaken || Settings.ShowGuideDonutWithFirstCapture && WorkflowStage == WorkflowStage.Capture) && Settings.IsGuideDonutVisible;
        public bool ShouldRollGuideBeVisible => WorkflowStage == WorkflowStage.Capture && Settings.ShowRollGuide;
        public bool ShouldPitchGuideBeVisible => IsExactlyOnePictureTaken && Settings.ShowPitchGuide;
        public bool ShouldYawGuideBeVisible => IsExactlyOnePictureTaken && Settings.ShowYawGuide;
        public bool ShouldSaveEditsButtonBeVisible => WorkflowStage == WorkflowStage.Edits ||
                                                      WorkflowStage == WorkflowStage.Crop ||
                                                      WorkflowStage == WorkflowStage.Keystone ||
                                                      WorkflowStage == WorkflowStage.ManualAlign;
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
                                                               WorkflowStage == WorkflowStage.Final);
        public string PortraitToLandscapeHint =>
            WorkflowStage == WorkflowStage.Capture ? "(flip for landscape)" : "(flip to landscape for a better view)";

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

            SaveCapturesCommand = new Command(async () =>
            {
                WorkflowStage = WorkflowStage.Saving;
                
                try
                {
                    await Task.Run(async () =>
                    {
                        var needs180Flip = Device.RuntimePlatform == Device.iOS && IsViewInvertedLandscape;

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
                            LeftLeftCrop, LeftRightCrop, RightLeftCrop, RightRightCrop);
                        var borderThickness = Settings.AddBorder
                            ? (int) (DrawTool.BORDER_CONVERSION_FACTOR * Settings.BorderThicknessProportion *
                                     finalImageWidth)
                            : 0;
                        finalImageWidth += 4 * borderThickness;
                        var finalImageHeight = DrawTool.CalculateCanvasHeightLessBorder(LeftBitmap, RightBitmap,
                                                   LeftTopCrop, LeftBottomCrop, RightTopCrop, RightBottomCrop,
                                                   ManualAlignment) +
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
                                    Settings.BorderThicknessProportion, Settings.AddBorder, Settings.BorderColor,
                                    LeftLeftCrop, LeftRightCrop, RightLeftCrop, RightRightCrop,
                                    LeftTopCrop, LeftBottomCrop, RightTopCrop, RightBottomCrop,
                                    LeftRotation, RightRotation,
                                    ManualAlignment,
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
                                    Settings.BorderThicknessProportion, Settings.AddBorder, Settings.BorderColor,
                                    LeftLeftCrop, LeftRightCrop, RightLeftCrop, RightRightCrop,
                                    LeftTopCrop, LeftBottomCrop, RightTopCrop, RightBottomCrop,
                                    LeftRotation, RightRotation,
                                    ManualAlignment,
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
                var stopwatch = new Stopwatch();

                SKBitmap alignedResult = null;
                try
                {
                    stopwatch.Start();
                    await Task.Run(() =>
                    {
                        alignedResult = openCv.CreateAlignedSecondImage(
                            IsCaptureLeftFirst ? LeftBitmap : RightBitmap,
                            IsCaptureLeftFirst ? RightBitmap : LeftBitmap,
                            Settings.AutomaticAlignmentDownsizePercentage,
                            Settings.AutomaticAlignmentIterations,
                            Settings.AutomaticAlignmentEpsilonLevel,
                            Settings.AutomaticAlignmentEccThresholdPercentage);
                    });
                    stopwatch.Stop();
                    Debug.WriteLine(stopwatch.ElapsedMilliseconds + "," + 
                                    Settings.AutomaticAlignmentDownsizePercentage + "," +
                                    Settings.AutomaticAlignmentIterations + "," +
                                    Settings.AutomaticAlignmentEpsilonLevel);
                }
                catch (Exception e)
                {
                    ErrorMessage = e.ToString();
                }

                if (alignedResult != null)
                {
                    if (IsCaptureLeftFirst)
                    {
                        SetRightBitmap(alignedResult);
                    }
                    else
                    {
                        SetLeftBitmap(alignedResult);
                    }
                }
                else
                {
                    AlignmentFailFadeTrigger = !AlignmentFailFadeTrigger;
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
                if (Settings.IsAutomaticAlignmentOn)
                {
                    AutoAlignIfNotYetRun();
                }
            }
        }

        private static SKBitmap GetHalfOfFullStereoImage(byte[] bytes, bool wantLeft)
        {
            var original = SKBitmap.Decode(bytes);

            var width = (int) Math.Round(original.Width / 2f);
            var height = original.Height;

            var extracted = new SKBitmap(width, height);

            using (var surface = new SKCanvas(extracted))
            {
                surface.DrawBitmap(
                    original,
                    SKRect.Create(wantLeft ? 0 : width, 0, width, height),
                    SKRect.Create(0, 0, width, height));
            }

            return extracted;
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
            LeftLeftCrop = 0;
            LeftRightCrop = 0;
            RightLeftCrop = 0;
            RightRightCrop = 0;
            LeftTopCrop = 0;
            LeftBottomCrop = 0;
            RightTopCrop = 0;
            RightBottomCrop = 0;
        }

        private void ClearAlignments()
        {
            LeftRotation = 0;
            RightRotation = 0;
            LeftZoom = 0;
            RightZoom = 0;
            ManualAlignment = 0;
        }

        private void ClearKeystone()
        {
            LeftKeystone = 0;
            RightKeystone = 0;
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