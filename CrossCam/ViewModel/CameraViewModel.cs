using System;
using System.IO;
using System.Runtime.CompilerServices;
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
        private const string FULL_IMAGE = "Load full stereo image";
        private const string SINGLE_SIDE = "Load single side";
        private const string CANCEL = "Cancel";

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

        private SKBitmap _originalUnalignedBitmap { get; set; }

        public bool IsCameraVisible { get; set; }
        public byte[] CapturedImageBytes { get; set; }
        public bool CaptureSuccess { get; set; }
        public int CameraColumn { get; set; }

        public double PreviewBottomY { get; set; }

        public Command CapturePictureCommand { get; set; }
        public bool CapturePictureTrigger { get; set; }

        public bool MoveLeftTrigger { get; set; }
        public bool MoveRightTrigger { get; set; }
        public bool WasSwipedTrigger { get; set; }

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

        public float RotationMax => 5;
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
        public bool IsFocusCircleVisible { get; set; }
        public bool IsFocusCircleLocked { get; set; }
        public double FocusCircleX { get; set; }
        public double FocusCircleY { get; set; }

        public bool IsNothingCaptured => LeftBitmap == null && RightBitmap == null;
        public bool ShouldIconBeVisible => IsNothingCaptured && IconColumn != CameraColumn && WorkflowStage == WorkflowStage.Capture;
        public bool ShouldLeftLeftRetakeBeVisible => LeftBitmap != null && (WorkflowStage == WorkflowStage.Final && DoesCaptureOrientationMatchViewOrientation || WorkflowStage == WorkflowStage.Capture && (Settings.Handedness == Handedness.Right || Settings.Handedness == Handedness.Center));
        public bool ShouldLeftRightRetakeBeVisible => LeftBitmap != null && WorkflowStage == WorkflowStage.Capture && Settings.Handedness == Handedness.Left;
        public bool ShouldRightLeftRetakeBeVisible => RightBitmap != null && WorkflowStage == WorkflowStage.Capture && Settings.Handedness == Handedness.Right;
        public bool ShouldRightRightRetakeBeVisible => RightBitmap != null && (WorkflowStage == WorkflowStage.Final && DoesCaptureOrientationMatchViewOrientation || WorkflowStage == WorkflowStage.Capture && (Settings.Handedness == Handedness.Left || Settings.Handedness == Handedness.Center));
        public bool ShouldCenterLoadBeVisible => WorkflowStage == WorkflowStage.Capture && Settings.Handedness != Handedness.Center;
        public bool ShouldLeftLoadBeVisible => CameraColumn == 0 && WorkflowStage == WorkflowStage.Capture && Settings.Handedness == Handedness.Center;
        public bool ShouldRightLoadBeVisible => CameraColumn == 1 && WorkflowStage == WorkflowStage.Capture && Settings.Handedness == Handedness.Center;
        public bool DoesCaptureOrientationMatchViewOrientation => WasCapturePortrait == IsViewPortrait;
        public bool ShouldSettingsAndHelpBeVisible => !IsBusy && 
                                                      WorkflowStage != WorkflowStage.View;
        public bool IsExactlyOnePictureTaken => LeftBitmap == null ^ RightBitmap == null;
        public bool ShouldRightCaptureBeVisible => WorkflowStage == WorkflowStage.Capture && Settings.Handedness == Handedness.Right;
        public bool ShouldCenterCaptureBeVisible => WorkflowStage == WorkflowStage.Capture && Settings.Handedness == Handedness.Center;
        public bool ShouldLeftCaptureBeVisible => WorkflowStage == WorkflowStage.Capture && Settings.Handedness == Handedness.Left;
        public bool ShouldLineGuidesBeVisible => (IsExactlyOnePictureTaken || Settings.ShowGuideLinesWithFirstCapture && WorkflowStage == WorkflowStage.Capture) && Settings.AreGuideLinesVisible || WorkflowStage == WorkflowStage.Keystone || WorkflowStage == WorkflowStage.ManualAlign;
        public bool ShouldDonutGuideBeVisible => (IsExactlyOnePictureTaken || Settings.ShowGuideDonutWithFirstCapture && WorkflowStage == WorkflowStage.Capture) && Settings.IsGuideDonutVisible;
        public bool ShouldRollGuideBeVisible => WorkflowStage == WorkflowStage.Capture && Settings.ShowRollGuide;
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
                                                          Settings.SaveRedundantFirstSide ||
                                                          Settings.RedCyanAnaglyphMode);
        
        public int IconColumn => IsCaptureLeftFirst ? 1 : 0;

        public bool ShouldPortraitViewModeWarningBeVisible => IsViewPortrait &&
                                                              WorkflowStage != WorkflowStage.Saving &&
                                                              (IsNothingCaptured ||
                                                               WorkflowStage == WorkflowStage.Final ||
                                                               WorkflowStage == WorkflowStage.Edits);
        public string PortraitToLandscapeHint =>
            WorkflowStage == WorkflowStage.Capture ? "(flip for landscape)" : WorkflowStage == WorkflowStage.Edits ? "(flip to landscape for easier editing)" : "(flip to landscape for a better view)";

        public string SavedSuccessMessage => "Saved to " + (Settings.SaveToExternal
                                                 ? "external"
                                                 :
                                                 !string.IsNullOrWhiteSpace(Settings.SavingDirectory)
                                                     ?
                                                     "custom folder"
                                                     : "Photos") + "!";

        private WorkflowStage _stageBeforeView;
        private int _alignmentThreadLock;
        private bool _wasAlignmentWithHorizontalRun;
        private bool _wasAlignmentWithoutHorizontalRun;
        private readonly IPhotoSaver _photoSaver;

        public CameraViewModel()
        {
            _photoSaver = DependencyService.Get<IPhotoSaver>();
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
                        LeftBytesCaptured(CapturedImageBytes); // not awaiting. ok.
                    }
                    else
                    {
                        RightBytesCaptured(CapturedImageBytes); // not awaiting. ok.
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
                    var alignmentWasRunButIsOffNow = !Settings.IsAutomaticAlignmentOn &&
                                                      (_wasAlignmentWithoutHorizontalRun ||
                                                       _wasAlignmentWithHorizontalRun);
                    var otherAlignmentModeWasRun = Settings.IsAutomaticAlignmentOn &&
                                                   (_wasAlignmentWithHorizontalRun && !Settings.RedCyanAnaglyphMode && !Settings.AlignHorizontallySideBySide ||
                                                    _wasAlignmentWithoutHorizontalRun && (Settings.RedCyanAnaglyphMode || Settings.AlignHorizontallySideBySide));
                    if (alignmentWasRunButIsOffNow ||
                        otherAlignmentModeWasRun)
                    {
                        ClearCrops(true);
                        if (IsCaptureLeftFirst)
                        {
                            SetRightBitmap(_originalUnalignedBitmap); //calls autoalign internally
                        }
                        else
                        {
                            SetLeftBitmap(_originalUnalignedBitmap); //calls autoalign internally
                        }
                    }

                    AutoAlign();
                }
                else if (args.PropertyName == nameof(WasSwipedTrigger))
                {
                    SwapSidesCommand.Execute(null);
                }
            };

            LoadPhotoCommand = new Command(async () =>
            {
                var photos = await DependencyService.Get<IPhotoPicker>().GetImages();
                if (photos != null)
                {
                    LoadSharedImages(photos[0], photos[1]);
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
                        ClearCrops(false);
                        break;
                    case WorkflowStage.ManualAlign:
                        ClearAlignments();
                        break;
                    case WorkflowStage.Keystone:
                        ClearKeystone();
                        break;
                }
            });

            ClearCapturesCommand = new Command(async() =>
            {
                var confirmClear = await CoreMethods.DisplayAlert("Really clear?",
                    "Are you sure you want to clear your pictures and start over?", "Yes, clear", "No");
                if (confirmClear)
                {
                    ClearCaptures();
                }
            });

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
                if (WorkflowStage == WorkflowStage.Capture ||
                    WorkflowStage == WorkflowStage.Final ||
                    WorkflowStage == WorkflowStage.Edits)
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

                    var tempCrop = InsideCrop;
                    InsideCrop = OutsideCrop;
                    OutsideCrop = tempCrop;

                    Settings.IsCaptureLeftFirst = IsCaptureLeftFirst;
                    PersistentStorage.Save(PersistentStorage.SETTINGS_KEY, Settings);
                }
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

                                SKBitmap innerLeftBitmap;
                                SKBitmap innerRightBitmap;
                                if (IsCaptureLeftFirst)
                                {
                                    innerLeftBitmap = LeftBitmap;
                                    if (_wasAlignmentWithHorizontalRun ||
                                        _wasAlignmentWithoutHorizontalRun)
                                    {
                                        innerRightBitmap = _originalUnalignedBitmap;
                                    }
                                    else
                                    {
                                        innerRightBitmap = RightBitmap;
                                    }
                                }
                                else
                                {
                                    innerRightBitmap = RightBitmap;
                                    if (_wasAlignmentWithHorizontalRun ||
                                        _wasAlignmentWithoutHorizontalRun)
                                    {
                                        innerLeftBitmap = _originalUnalignedBitmap;
                                    }
                                    else
                                    {
                                        innerLeftBitmap = LeftBitmap;
                                    }
                                }

                                canvas.DrawBitmap(innerLeftBitmap, 0, 0);

                                await SaveSurfaceSnapshot(tempSurface);

                                canvas.Clear();

                                canvas.DrawBitmap(innerRightBitmap, 0, 0);

                                await SaveSurfaceSnapshot(tempSurface);
                            }
                        }

                        if (Settings.RedCyanAnaglyphMode)
                        {
                            var canvasWidth = LeftBitmap.Width - LeftCrop - InsideCrop - OutsideCrop - RightCrop;
                            var canvasHeight = DrawTool.CalculateCanvasHeightLessBorder(LeftBitmap, RightBitmap,
                                TopCrop, BottomCrop, VerticalAlignment);
                            using (var tempSurface =
                                SKSurface.Create(new SKImageInfo(canvasWidth, canvasHeight)))
                            {
                                var canvas = tempSurface.Canvas;
                                canvas.Clear(SKColor.Empty);

                                DrawTool.DrawImagesOnCanvas(canvas, LeftBitmap, RightBitmap,
                                    Settings.BorderWidthProportion, Settings.AddBorder, Settings.BorderColor,
                                    LeftCrop + OutsideCrop, InsideCrop + RightCrop, InsideCrop + LeftCrop,
                                    RightCrop + OutsideCrop,
                                    TopCrop, BottomCrop, LeftRotation, RightRotation,
                                    VerticalAlignment, LeftZoom, RightZoom,
                                    LeftKeystone, RightKeystone, DrawMode.RedCyan);

                                await SaveSurfaceSnapshot(tempSurface);
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

                                await SaveSurfaceSnapshot(tempSurface);
                            }
                        }

                        var finalImageWidth = DrawTool.CalculateJoinedCanvasWidthLessBorder(LeftBitmap, RightBitmap,
                            LeftCrop + OutsideCrop, InsideCrop + RightCrop, InsideCrop + LeftCrop,
                            RightCrop + OutsideCrop);
                        var borderThickness = Settings.AddBorder
                            ? (int) (DrawTool.BORDER_CONVERSION_FACTOR * Settings.BorderWidthProportion *
                                     finalImageWidth)
                            : 0;
                        finalImageWidth += 3 * borderThickness;
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
                                    LeftCrop + OutsideCrop, InsideCrop + RightCrop, InsideCrop + LeftCrop,
                                    RightCrop + OutsideCrop,
                                    TopCrop, BottomCrop,
                                    LeftRotation, RightRotation,
                                    VerticalAlignment,
                                    LeftZoom, RightZoom,
                                    LeftKeystone, RightKeystone);

                                await SaveSurfaceSnapshot(tempSurface);
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
                                    LeftCrop + OutsideCrop, InsideCrop + RightCrop, InsideCrop + LeftCrop,
                                    RightCrop + OutsideCrop,
                                    TopCrop, BottomCrop,
                                    LeftRotation, RightRotation,
                                    VerticalAlignment,
                                    LeftZoom, RightZoom,
                                    LeftKeystone, RightKeystone,
                                    DrawMode.Parallel);

                                await SaveSurfaceSnapshot(tempSurface);
                            }
                        }
                    });
                }
                catch (DirectoryNotFoundException)
                {
                    SaveFailFadeTrigger = !SaveFailFadeTrigger;
                    WorkflowStage = WorkflowStage.Final;

                    await CoreMethods.DisplayAlert("Directory Not Found",
                        "The save destination could not be found. Please choose another on the settings page.", "OK");

                    return;
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
                var sendReport = await CoreMethods.DisplayAlert("Oops",
                    "Sorry, CrossCam did an error. Please send me an error report so I can fix it!", "Send", "Don't Send");
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

        public bool BackButtonPressed()
        {
            switch (WorkflowStage)
            {
                case WorkflowStage.Final:
                    ClearCapturesCommand.Execute(this);
                    return true;
                case WorkflowStage.View:
                    ToggleViewModeCommand.Execute(this);
                    return true;
                case WorkflowStage.Crop:
                case WorkflowStage.ManualAlign:
                case WorkflowStage.Keystone:
                case WorkflowStage.Edits:
                    SaveEditCommand.Execute(this);
                    return true;
                case WorkflowStage.Capture:
                    return false;
                case WorkflowStage.Saving:
                case WorkflowStage.AutomaticAlign:
                case WorkflowStage.Loading:
                default:
                    return true;
            }
        }

        public async void LoadSharedImages(byte[] image1, byte[] image2)
        {
            try
            {
                WorkflowStage = WorkflowStage.Loading;

                if (image2 == null)
                {
                    await Task.Delay(2000);
                    var loadType = await OpenLoadingPopup();

                    if (loadType == CANCEL ||
                        loadType == null)
                    {
                        WorkflowStage = WorkflowStage.Capture;
                        return;
                    }

                    if (loadType == SINGLE_SIDE)
                    {
                        if (LeftBitmap == null &&
                            RightBitmap != null)
                        {
                            await LeftBytesCaptured(image1);
                            return;
                        }

                        if (RightBitmap == null &&
                            LeftBitmap != null)
                        {
                            await RightBytesCaptured(image1);
                            return;
                        }

                        if (RightBitmap == null &&
                            LeftBitmap == null)
                        {
                            CapturedImageBytes = image1;
                            return;
                        }
                    }
                    else
                    {
                        await LoadFullStereoImage(image1);
                        return;
                    }
                }
                else
                {
                    // i save left first, so i load left first
                    await LeftBytesCaptured(image1);
                    await RightBytesCaptured(image2);
                }

            }
            catch (Exception e)
            {
                ErrorMessage = e.ToString();
            }
        }

        protected override async void ViewIsAppearing(object sender, EventArgs e)
        {
            base.ViewIsAppearing(sender, e);
            RaisePropertyChanged(nameof(ShouldLineGuidesBeVisible)); //TODO: figure out how to have Fody do this (just firing 'null' has bad behavior)
            RaisePropertyChanged(nameof(ShouldDonutGuideBeVisible));
            RaisePropertyChanged(nameof(ShouldRollGuideBeVisible));
            RaisePropertyChanged(nameof(ShouldSaveCapturesButtonBeVisible));
            RaisePropertyChanged(nameof(ShouldLeftLeftRetakeBeVisible));
            RaisePropertyChanged(nameof(ShouldLeftRightRetakeBeVisible));
            RaisePropertyChanged(nameof(ShouldRightLeftRetakeBeVisible));
            RaisePropertyChanged(nameof(ShouldRightRightRetakeBeVisible));
            RaisePropertyChanged(nameof(ShouldLeftCaptureBeVisible));
            RaisePropertyChanged(nameof(ShouldCenterCaptureBeVisible));
            RaisePropertyChanged(nameof(ShouldRightCaptureBeVisible));
            RaisePropertyChanged(nameof(ShouldCenterLoadBeVisible));
            RaisePropertyChanged(nameof(ShouldLeftLoadBeVisible));
            RaisePropertyChanged(nameof(ShouldRightLoadBeVisible));
            RaisePropertyChanged(nameof(SavedSuccessMessage));
            RaisePropertyChanged(nameof(Settings)); // this doesn't cause reevaluation for above stuff (but I'd like it to), but it does trigger redraw of canvas and evaluation of whether to run auto alignment

            await Task.Delay(100);
            await EvaluateAndShowWelcomePopup();
        }

        private async Task SaveSurfaceSnapshot(SKSurface surface)
        {
            using (var skImage = surface.Snapshot())
            {
                using (var encoded = skImage.Encode(SKEncodedImageFormat.Jpeg, 100))
                {
                    await _photoSaver.SavePhoto(encoded.ToArray(), Settings.SavingDirectory, Settings.SaveToExternal);
                }
            }
        }

        private async Task<string> OpenLoadingPopup()
        {
            return await CoreMethods.DisplayActionSheet("Choose an action:", CANCEL, null,
                FULL_IMAGE, SINGLE_SIDE);
        }

        private async Task LoadFullStereoImage(byte[] image)
        {
            try
            {
                var leftHalf = await Task.Run(() => GetHalfOfFullStereoImage(image, true, Settings.ClipBorderOnLoad));
                SetLeftBitmap(leftHalf, false);
                var rightHalf = await Task.Run(() => GetHalfOfFullStereoImage(image, false, Settings.ClipBorderOnLoad));
                SetRightBitmap(rightHalf, false);
            }
            catch (Exception e)
            {
                ErrorMessage = e.ToString();
            }
        }

        private async Task LeftBytesCaptured(byte[] capturedBytes)
        {
            var bitmap = await Task.Run(() => GetBitmapAndCorrectOrientation(capturedBytes));
            SetLeftBitmap(bitmap);
        }

        private async Task RightBytesCaptured(byte[] capturedBytes)
        {
            var bitmap = await Task.Run(() => GetBitmapAndCorrectOrientation(capturedBytes));
            SetRightBitmap(bitmap);
        }

        private async void AutoAlign()
        {
            if (Settings.IsAutomaticAlignmentOn &&
                LeftBitmap != null &&
                RightBitmap != null &&
                !_wasAlignmentWithHorizontalRun &&
                !_wasAlignmentWithoutHorizontalRun &&
                0 == Interlocked.Exchange(ref _alignmentThreadLock, 1))
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
                                Settings.AlignmentDownsizePercentage2,
                                Settings.AlignmentIterations2,
                                Settings.AlignmentEpsilonLevel2,
                                Settings.AlignmentEccThresholdPercentage2,
                                Settings.AlignmentPyramidLayers2,
                                !Settings.RedCyanAnaglyphMode && !Settings.AlignHorizontallySideBySide);
                        });
                    }
                    catch (Exception e)
                    {
                        ErrorMessage = e.ToString();
                    }

                    if (alignedResult != null)
                    {
                        _wasAlignmentWithHorizontalRun = Settings.RedCyanAnaglyphMode || Settings.AlignHorizontallySideBySide;
                        _wasAlignmentWithoutHorizontalRun = !Settings.RedCyanAnaglyphMode && !Settings.AlignHorizontallySideBySide;

                        var topLeft = alignedResult.TransformMatrix.MapPoint(0, 0);
                        var topRight = alignedResult.TransformMatrix.MapPoint(alignedResult.AlignedBitmap.Width - 1, 0);
                        var bottomRight = alignedResult.TransformMatrix.MapPoint(alignedResult.AlignedBitmap.Width - 1,
                            alignedResult.AlignedBitmap.Height - 1);
                        var bottomLeft =
                            alignedResult.TransformMatrix.MapPoint(0, alignedResult.AlignedBitmap.Height - 1);

                        //this actually cuts off a bit more than it has to, but it is inconsequential for small deviations
                        //(it cuts at the corner of the original image, not at the point where the original border crosses the new border)

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

                        if (topLeft.X > bottomLeft.X)
                        {
                            if (topLeft.X > 0)
                            {
                                LeftCrop = (int) topLeft.X;
                            }
                        }
                        else
                        {
                            if (bottomLeft.X > 0)
                            {
                                LeftCrop = (int) bottomLeft.X;
                            }
                        }

                        var maxX = alignedResult.AlignedBitmap.Width - 1;
                        if (topRight.X < bottomRight.X)
                        {
                            if (topRight.X < maxX)
                            {
                                RightCrop = (int) (maxX - topRight.X);
                            }
                        }
                        else
                        {
                            if (bottomRight.X < maxX)
                            {
                                RightCrop = (int) (maxX - bottomRight.X);
                            }
                        }

                        if (IsCaptureLeftFirst)
                        {
                            _originalUnalignedBitmap = RightBitmap;
                            SetRightBitmap(alignedResult.AlignedBitmap);
                        }
                        else
                        {
                            _originalUnalignedBitmap = LeftBitmap;
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

                _alignmentThreadLock = 0;
            }
        }

        private void SetLeftBitmap(SKBitmap bitmap, bool withMovementTrigger = true)
        {
            if (bitmap == null) return;

            LeftBitmap = bitmap;
            WasCapturePortrait = LeftBitmap.Width < LeftBitmap.Height;

            if (RightBitmap == null)
            {
                if (withMovementTrigger)
                {
                    MoveLeftTrigger = !MoveLeftTrigger;
                }
                CameraColumn = 1;
                WorkflowStage = WorkflowStage.Capture;
            }
            else
            {
                CameraColumn = IsCaptureLeftFirst ? 0 : 1;
                IsCameraVisible = false;
                WorkflowStage = WorkflowStage.Final;
                SetMaxEdits(bitmap);
                AutoAlign();
            }
        }

        private void SetRightBitmap(SKBitmap bitmap, bool withMovementTrigger = true)
        {
            if (bitmap == null) return;

            RightBitmap = bitmap;
            WasCapturePortrait = RightBitmap.Width < RightBitmap.Height;

            if (LeftBitmap == null)
            {
                if (withMovementTrigger)
                {
                    MoveRightTrigger = !MoveRightTrigger;
                }
                CameraColumn = 0;
                WorkflowStage = WorkflowStage.Capture;
            }
            else
            {
                CameraColumn = IsCaptureLeftFirst ? 0 : 1;
                IsCameraVisible = false;
                WorkflowStage = WorkflowStage.Final;
                SetMaxEdits(bitmap);
                AutoAlign();
            }
        }

        private void SetMaxEdits(SKBitmap bitmap)
        {
            SideCropMax = bitmap.Width / 2;
            TopOrBottomCropMax = bitmap.Height / 2;
            VerticalAlignmentMax = bitmap.Height / 8;
            ZoomMax = bitmap.Height / 12;
        }

        private static SKBitmap GetHalfOfFullStereoImage(byte[] bytes, bool wantLeft, bool clipBorder) 
        {
            var original = SKBitmap.Decode(bytes);

            const int BORDER_DIFF_THRESHOLD = 25;
            var bottomBorder = 0;
            var leftBorder = 0;
            var topBorder = 0;
            var rightBorder = 0;

            var midX = original.Width / 2;
            if (original.Width % 2 == 0 &&
                wantLeft)
            {
                midX -= 1;
            }
            var startX = wantLeft ? 0 : midX;
            var endX = wantLeft ? midX : original.Width - 1;
            var startY = 0;
            var endY = original.Height - 1;

            if (clipBorder)
            {
                var topLeftColor = GetTotalColor(original.GetPixel(startX, startY));
                var topRightColor = GetTotalColor(original.GetPixel(endX, startY));
                var bottomRightColor = GetTotalColor(original.GetPixel(endX, endY));
                var bottomLeftColor = GetTotalColor(original.GetPixel(startX, endY));

                if (Math.Abs(topLeftColor - topRightColor) < BORDER_DIFF_THRESHOLD &&
                    Math.Abs(topRightColor - bottomRightColor) < BORDER_DIFF_THRESHOLD &&
                    Math.Abs(bottomRightColor - bottomLeftColor) < BORDER_DIFF_THRESHOLD &&
                    Math.Abs(bottomLeftColor - topLeftColor) < BORDER_DIFF_THRESHOLD &&
                    Math.Abs(topLeftColor - bottomRightColor) < BORDER_DIFF_THRESHOLD &&
                    Math.Abs(topRightColor - bottomLeftColor) < BORDER_DIFF_THRESHOLD)
                {
                    for (var ii = startX; ii < startX + (endX - startX) / 2; ii++)
                    {
                        var color = original.GetPixel(ii, endY / 2);
                        if (Math.Abs(color.Red + color.Green + color.Blue - topLeftColor) > BORDER_DIFF_THRESHOLD)
                        {
                            leftBorder = ii - startX;
                            break;
                        }
                    }
                    for (var ii = startY; ii < endY / 2; ii++)
                    {
                        var color = original.GetPixel(startX + (endX - startX) / 2, ii);
                        if (Math.Abs(color.Red + color.Green + color.Blue - topLeftColor) > BORDER_DIFF_THRESHOLD)
                        {
                            topBorder = ii - startY;
                            break;
                        }
                    }
                    for (var ii = endX; ii > startX + (endX - startX) / 2; ii--)
                    {
                        var color = original.GetPixel(ii, endY / 2);
                        if (Math.Abs(color.Red + color.Green + color.Blue - topLeftColor) > BORDER_DIFF_THRESHOLD)
                        {
                            rightBorder = endX - ii;
                            break;
                        }
                    }
                    for (var ii = endY; ii > endY / 2; ii--)
                    {
                        var color = original.GetPixel(startX + (endX - startX) / 2, ii);
                        if (Math.Abs(color.Red + color.Green + color.Blue - topLeftColor) > BORDER_DIFF_THRESHOLD)
                        {
                            bottomBorder = endY - ii;
                            break;
                        }
                    }
                }
            }

            var width = endX - startX - rightBorder - leftBorder;
            var height = endY - startY - topBorder - bottomBorder;

            var extracted = new SKBitmap(width, height);

            using (var surface = new SKCanvas(extracted))
            {
                surface.DrawBitmap(
                    original,
                    SKRect.Create(
                        startX + leftBorder,
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

        private SKBitmap GetBitmapAndCorrectOrientation(byte[] bytes)
        {
            try
            {
                SKCodecOrigin origin = 0;

                using (var stream = new MemoryStream(bytes))
                using (var data = SKData.Create(stream))
                using (var codec = SKCodec.Create(data))
                {
                    if (codec != null)
                    {
                        origin = codec.Origin;
                    }
                }

                switch (origin)
                {
                    case SKCodecOrigin.BottomRight:
                        return BitmapRotate180(SKBitmap.Decode(bytes));
                    case SKCodecOrigin.RightTop:
                        return BitmapRotate90(SKBitmap.Decode(bytes));
                    case SKCodecOrigin.LeftBottom:
                        return BitmapRotate270(SKBitmap.Decode(bytes));
                    default:
                        return SKBitmap.Decode(bytes);
                }
            }
            catch (Exception e)
            {
                Device.BeginInvokeOnMainThread(() =>
                {
                    ErrorMessage = e.ToString();
                });
                return null;
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

        private static SKBitmap BitmapRotate270(SKBitmap originalBitmap)
        {
            var rotated = new SKBitmap(originalBitmap.Height, originalBitmap.Width);

            using (var surface = new SKCanvas(rotated))
            {
                surface.Translate(0, rotated.Height);
                surface.RotateDegrees(270);
                surface.DrawBitmap(originalBitmap, 0, 0);
            }

            return rotated;
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

        private void ClearCrops(bool andAutomaticAlignmentFlags)
        {
            OutsideCrop = 0;
            InsideCrop = 0;
            RightCrop = 0;
            LeftCrop = 0;
            TopCrop = 0;
            BottomCrop = 0;
            CropMode = CropMode.Inside;
            if (andAutomaticAlignmentFlags)
            {
                _wasAlignmentWithoutHorizontalRun = false;
                _wasAlignmentWithHorizontalRun = false;
            }
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
            ClearCrops(true);
            ClearAlignments();
            ClearKeystone();
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

            if (Settings.IsTapToFocusEnabled2)
            {
                SwitchToContinuousFocusTrigger = !SwitchToContinuousFocusTrigger;
            }
        }
    }
}