using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Timers;
#if RELEASE
using System.Web;
#endif
using CrossCam.CustomElement;
using CrossCam.Model;
using CrossCam.Page;
using CrossCam.Resources.Localization;
using CrossCam.Wrappers;
using Newtonsoft.Json;
using SkiaSharp;
using DeviceInfo = Microsoft.Maui.Devices.DeviceInfo;
using ErrorEventArgs = CrossCam.CustomElement.ErrorEventArgs;
using Exception = System.Exception;
using Rect = Microsoft.Maui.Graphics.Rect;
using Microsoft.Maui.Layouts;
using NewRelic.MAUI.Plugin;

namespace CrossCam.ViewModel
{
    public sealed class CameraViewModel : BaseViewModel
    {
        private readonly string _fullImage = AppResources.Page_Camera_LoadFullImage;
        private readonly string _singleSide = AppResources.Page_Camera_LoadSingleSide;
        private readonly string _cancel = AppResources.Page_Camera_Cancel;
        private const string CROSSCAM = "CrossCam"; 
        private const string COMMAND_ANALYTICS_EVENT = "command_start";
        private const string COMMAND_ANALYTICS_KEY_NAME = "command_name";
        private const int BORDER_DIFF_THRESHOLD = 25;

        public static PairOperator PairOperator;
        public PairOperator PairOperatorBindable => PairOperator;

        public DisplayOrientation DisplayOrientation { get; set; }
        public DisplayRotation DisplayRotation { get; set; }

        public WorkflowStage WorkflowStage { get; set; }
        public CropMode CropMode { get; set; }
        public ManualAlignMode ManualAlignMode { get; set; }
        public CameraSettingMode CameraSettingMode { get; set; }
        public FovCorrectionMode FovCorrectionMode { get; set; }

        public bool CaptureSuccessTrigger { get; set; }

        private SKMatrix? _leftAlignmentTransformCardboardCached;
        public SKMatrix? LeftAlignmentTransformCardboardCached
        {
            get
            {
                if (Settings.AlignmentSettings.IsAutomaticAlignmentOn)
                {
                    return _leftAlignmentTransformCardboardCached;
                }
                return SKMatrix.Identity;
            }
            set => _leftAlignmentTransformCardboardCached = value;
        }
        private SKMatrix _leftAlignmentTransform;
        public SKMatrix LeftAlignmentTransform
        {
            get
            {
                if (Settings.AlignmentSettings.IsAutomaticAlignmentOn)
                {
                    return _leftAlignmentTransform;
                }
                return SKMatrix.Identity;
            }
            set
            {
                _leftAlignmentTransform = value;
                LeftAlignmentTransformCardboardCached = null;
            }
        }
        private SKBitmap _leftCapture;
        public SKBitmap LeftCapture
        {
            get => _leftCapture;
            set
            {
                _leftCapture = value;
                LeftCaptureCardboardCached = null;
            }
        }
        public SKBitmap LeftCaptureCardboardCached { get; private set; }
        public Command RetakeLeftCommand { get; set; }

        private SKMatrix? _rightAlignmentTransformCardboardCached;
        public SKMatrix? RightAlignmentTransformCardboardCached
        {
            get
            {
                if (Settings.AlignmentSettings.IsAutomaticAlignmentOn)
                {
                    return _rightAlignmentTransformCardboardCached;
                }
                return SKMatrix.Identity;
            }
            set => _rightAlignmentTransformCardboardCached = value;
        }
        private SKMatrix _rightAlignmentTransform;
        public SKMatrix RightAlignmentTransform
        {
            get
            {
                if (Settings.AlignmentSettings.IsAutomaticAlignmentOn)
                {
                    return _rightAlignmentTransform;
                }
                return SKMatrix.Identity;
            }
            set
            {
                _rightAlignmentTransform = value;
                RightAlignmentTransformCardboardCached = null;
            }
        }
        private SKBitmap _rightCapture;
        public SKBitmap RightCapture
        {
            get => _rightCapture;
            set
            {
                _rightCapture = value;
                RightCaptureCardboardCached = null;
            }
        }
        public SKBitmap RightCaptureCardboardCached { get; private set; }
        public Command RetakeRightCommand { get; set; }

        public string AlignmentConfidence { get; set; }

        public bool CaptureSuccess { get; set; }
        public int CameraColumn { get; set; }
        
        public IncomingFrame RemotePreviewFrame { get; set; }
        public IncomingFrame LocalPreviewFrame { get; set; }
        public IncomingFrame LocalCapturedFrame { get; set; }

        private bool UseFullScreenWidth => Settings.Mode != DrawMode.Parallel ||
                                           WorkflowStage == WorkflowStage.Capture &&
                                           Settings.FullscreenCapturing ||
                                           WorkflowStage != WorkflowStage.Capture &&
                                           Settings.FullscreenEditing ||
                                           IsNothingCaptured && 
                                           !(Settings.Mode == DrawMode.Parallel && 
                                             Settings.IsCaptureInMirrorMode);
        public AbsoluteLayoutFlags CanvasRectangleFlags =>
            UseFullScreenWidth ? AbsoluteLayoutFlags.All : 
            AbsoluteLayoutFlags.YProportional | AbsoluteLayoutFlags.HeightProportional | AbsoluteLayoutFlags.XProportional;
        public Rect CanvasRectangle 
        {
            get 
            {
                if (UseFullScreenWidth) return new Rect(0,0,1,1);
                var mainPage = Application.Current?.MainPage;
                var windowWidth = int.MaxValue;
                if (mainPage != null &&
                    mainPage.Width > 0 &&
                    mainPage.Height > 0)
                {
                    var appWidth = mainPage.Width;
                    var appHeight = mainPage.Height;

                    windowWidth = (int) (IsViewPortrait ? Math.Min(appWidth, appHeight) : Math.Max(appWidth, appHeight));
                }
                return new Rect(
                    0.5, 
                    0, 
                    WorkflowStage == WorkflowStage.Capture && 
                    !Settings.FullscreenCapturing && 
                    WorkflowStage != WorkflowStage.Capture && 
                    !Settings.FullscreenEditing ? 
                        windowWidth : 
                        Math.Min(Settings.MaximumParallelWidth, windowWidth), 
                    1);
            }
        }

        public double PreviewAspectRatio { get; set; }

        public Command CapturePictureCommand { get; set; }
        public bool CapturePictureTrigger { get; set; }
        
        public bool MoveHintTriggerCenter { get; set; }
        public bool MoveHintTriggerSide { get; set; }
        public bool WasSwipedTrigger { get; set; }
        public bool ExploreReminderTrigger { get; set; }

        public Command ToggleFullscreen { get; set; }
        public bool IsFullscreenToggleVisible =>
            (Settings.Mode == DrawMode.Cross ||
             Settings.Mode == DrawMode.Parallel ||
             Settings.Mode == DrawMode.Cardboard &&
             WorkflowStage != WorkflowStage.Capture) &&
            (!IsNothingCaptured ||
            PairOperator.PairStatus == PairStatus.Connected &&
            Settings.PairSettings.IsPairedPrimary.HasValue &&
            Settings.PairSettings.IsPairedPrimary.Value ||
            Settings.IsCaptureInMirrorMode);
        public bool IsFullscreenToggle
        {
            get =>
                WorkflowStage == WorkflowStage.Capture &&
                Settings.FullscreenCapturing && 
                Settings.Mode != DrawMode.Cardboard ||
                WorkflowStage != WorkflowStage.Capture &&
                Settings.FullscreenEditing;
            set
            {
                if (WorkflowStage == WorkflowStage.Capture)
                {
                    Settings.FullscreenCapturing = value;
                }
                else
                {
                    Settings.FullscreenEditing = value;
                }
                PersistentStorage.Save(PersistentStorage.SETTINGS_KEY,Settings);
            }
        }

        public Command SaveCapturesCommand { get; set; }

        public Command ToggleViewModeCommand { get; set; }

        public Command ClearCapturesCommand { get; set; }

        public Command OpenCameraSettingsCommand { get; set; }
        public Command SaveCameraSettingCommand { get; set; }
        public Command ResetCameraSettingCommand { get; set; }
        public Command<CameraSettingMode> SetCameraSettingModeCommand { get; set; }
        public bool CameraSettingsVisible { get; set; }
        public ObservableCollection<AvailableCamera> AvailableCameras { get; set; }
        public AvailableCamera ChosenCamera
        {
            get => Settings.ChosenCamera;
            set => Settings.ChosenCamera = value;
        }

        public Command NavigateToSettingsCommand { get; set; }
        public Command NavigateToHamburgerPageCommand { get; set; }

        public Command FlipCameraCommand { get; set; }

        public Command SwapSidesCommand { get; set; }

        public Command GoToModeCommand { get; set; }
        public Command SaveEditCommand { get; set; }

        public Command ClearEditCommand { get; set; }

        public Command ToggleAutoalignCommand { get; set; }

        public Command PromptForPermissionAndSendErrorEmailCommand { get; set; }
        public Exception Error { get; set; }

        public Settings Settings { get; set; }
        public int TotalSavesCompleted { get; set; }

        public Edits Edits { get; set; }
        public Explore Explore { get; set; }

        public Command SetManualAlignMode { get; set; }

        public Command SetFovCorrectionMode { get; set; }

        public Command PairCommand { get; set; }

        public double ZoomMax => Settings.EditsSettings.ZoomMax;
        public double SideCropMax => Settings.EditsSettings.SideCropMax;
        public double TopOrBottomCropMax => Settings.EditsSettings.TopOrBottomCropMax;

        public Command SetCropMode { get; set; }

        public double VerticalAlignmentMax => Settings.EditsSettings.VerticalAlignmentMax;
        public double VerticalAlignmentMin => -VerticalAlignmentMax;

        public float RotationMax => Settings.EditsSettings.RotationMax;
        public float RotationMin => -RotationMax;

        public float MaxKeystone => Settings.EditsSettings.KeystoneMax;
        public float MinKeystone => -MaxKeystone;

        public Command LoadPhotoCommand { get; set; }

        public bool IsViewPortrait => DisplayOrientation == DisplayOrientation.Portrait;
        public bool IsViewInverted => DisplayRotation == DisplayRotation.Rotation180 ||
                                      DisplayRotation == DisplayRotation.Rotation270;
        public bool WasCapturePortrait { get; set; }
        public bool WasCaptureCross { get; set; }
        public bool WasCapturePaired { get; set; }

        public bool AutomaticAlignmentNotSupportedTrigger { get; set; }
        public bool AlignmentFailFadeTrigger { get; set; }
        public bool SaveFailFadeTrigger { get; set; }
        public bool SaveSuccessFadeTrigger { get; set; }
        public bool MirrorModeAutoAlignWarningTrigger { get; set; }

        public bool SwitchToContinuousFocusTrigger { get; set; }
        public bool IsFocusCircleVisible { get; set; }
        public bool IsFocusCircleLocked { get; set; }
        public double FocusCircleX { get; set; }
        public double FocusCircleY { get; set; }

        public bool RestartPreviewTrigger { get; set; }
        public bool StopPreviewTrigger { get; set; }
        public bool IsNothingCaptured => LeftCapture == null && RightCapture == null;
        public bool AreBothSidesCaptured => LeftCapture != null && RightCapture != null;

        private bool _isClearPromptOpen;

        public bool ShouldPairButtonBeVisible => (IsNothingCaptured ||
                                                  PairOperator.PairStatus == PairStatus.Connected) &&
                                                 WorkflowStage != WorkflowStage.FovCorrection &&
                                                 WorkflowStage != WorkflowStage.ManualAlign &&
                                                 WorkflowStage != WorkflowStage.Crop &&
                                                 WorkflowStage != WorkflowStage.Keystone &&
                                                 !Settings.IsCaptureInMirrorMode;
        public Rect PairButtonPosition
        {
            get
            {
                var width = (double)Application.Current.Resources["_largeIconWidth"];
                var height = (double)Application.Current.Resources["_smallerButtonWidth"];
                double x;
                var captureButtonX = CaptureButtonPosition.X;
                if (captureButtonX == 0)
                {
                    x = 1;
                } 
                else if (captureButtonX == 1)
                {
                    x = 0;
                }
                else
                {
                    x = Settings.PairButtonHorizontalPosition == PairButtonHorizontalPosition.Left ? 0 : 1;
                }
                return new Rect(x, 1, width, height);
            }
        }

        public bool ShouldLeftLeftRetakeBeVisible => LeftCapture != null &&
                                                     (WorkflowStage == WorkflowStage.Final ||
                                                      WorkflowStage == WorkflowStage.Capture &&
                                                      (Settings.PortraitCaptureButtonPosition ==
                                                       PortraitCaptureButtonPosition.Right ||
                                                       Settings.PortraitCaptureButtonPosition ==
                                                       PortraitCaptureButtonPosition.Middle)) &&
                                                     !(WorkflowStage == WorkflowStage.Final &&
                                                      Settings.PairSettings.IsPairedPrimary.HasValue &&
                                                      Settings.PairSettings.IsPairedPrimary.Value &&
                                                      PairOperator.PairStatus == PairStatus.Connected);
        public bool ShouldLeftRightRetakeBeVisible => LeftCapture != null && 
                                                      WorkflowStage == WorkflowStage.Capture && 
                                                      Settings.PortraitCaptureButtonPosition == PortraitCaptureButtonPosition.Left ||
                                                      WorkflowStage == WorkflowStage.Final && 
                                                      Settings.PairSettings.IsPairedPrimary.HasValue && 
                                                      Settings.PairSettings.IsPairedPrimary.Value && 
                                                      PairOperator.PairStatus == PairStatus.Connected;
        public bool ShouldRightLeftRetakeBeVisible => RightCapture != null && 
                                                      WorkflowStage == WorkflowStage.Capture && 
                                                      Settings.PortraitCaptureButtonPosition == PortraitCaptureButtonPosition.Right;
        public bool ShouldRightRightRetakeBeVisible => RightCapture != null && 
                                                       (WorkflowStage == WorkflowStage.Final || 
                                                        WorkflowStage == WorkflowStage.Capture && 
                                                        (Settings.PortraitCaptureButtonPosition == PortraitCaptureButtonPosition.Left || 
                                                         Settings.PortraitCaptureButtonPosition == PortraitCaptureButtonPosition.Middle));
        
        public bool ShouldCenterLoadBeVisible => WorkflowStage == WorkflowStage.Capture && 
                                                 Settings.PortraitCaptureButtonPosition != PortraitCaptureButtonPosition.Middle && 
                                                 PairOperator.PairStatus == PairStatus.Disconnected;
        public bool ShouldLeftLoadBeVisible => CameraColumn == 0 && 
                                               WorkflowStage == WorkflowStage.Capture && 
                                               Settings.PortraitCaptureButtonPosition == PortraitCaptureButtonPosition.Middle && 
                                               PairOperator.PairStatus == PairStatus.Disconnected &&
                                               !Settings.IsCaptureInMirrorMode;
        public bool ShouldRightLoadBeVisible => CameraColumn == 1 && 
                                                WorkflowStage == WorkflowStage.Capture && 
                                                Settings.PortraitCaptureButtonPosition == PortraitCaptureButtonPosition.Middle && 
                                                PairOperator.PairStatus == PairStatus.Disconnected &&
                                                !Settings.IsCaptureInMirrorMode;
        public bool ShouldSwapSidesBeVisible => WorkflowStage == WorkflowStage.Capture &&
                                                (IsExactlyOnePictureTaken ||
                                                Settings.IsCaptureInMirrorMode ||
                                                PairOperatorBindable.PairStatus == PairStatus.Connected);
        public bool ShouldSettingsAndHelpBeVisible => !IsBusy && 
                                                      WorkflowStage != WorkflowStage.View;
        public bool IsExactlyOnePictureTaken => LeftCapture == null ^ RightCapture == null;
        public bool ShouldCaptureButtonBeVisible => WorkflowStage == WorkflowStage.Capture &&
                                                    PairOperatorBindable.PairStatus != PairStatus.Connecting &&
                                                    (PairOperatorBindable.PairStatus == PairStatus.Connected &&
                                                     Settings.PairSettings.IsPairedPrimary.HasValue && 
                                                     Settings.PairSettings.IsPairedPrimary.Value ||
                                                     PairOperatorBindable.PairStatus == PairStatus.Disconnected);

        public bool IsParallelTypeMode => 
            Settings.Mode == DrawMode.Parallel || 
            Settings.Mode == DrawMode.Cardboard;
        
        public Rect CaptureButtonPosition
        {
            get
            {
                var width = (double)Application.Current.Resources["_giantIconWidth"];
                double x = 0;
                double y = 0;
                if (Settings.Mode != DrawMode.Cardboard)
                {
                    if (IsViewPortrait)
                    {
                        y = 1;
                        switch (Settings.PortraitCaptureButtonPosition)
                        {
                            case PortraitCaptureButtonPosition.Right:
                                x = 1;
                                break;
                            case PortraitCaptureButtonPosition.Middle:
                                x = 0.5;
                                break;
                            case PortraitCaptureButtonPosition.Left:
                                x = 0;
                                break;
                        }
                    }
                    else
                    {
                        switch (Settings.LandscapeCaptureButtonVerticalPosition)
                        {
                            case LandscapeCaptureButtonVerticalPosition.Middle:
                                y = 0.5;
                                break;
                            case LandscapeCaptureButtonVerticalPosition.Bottom:
                                y = 1;
                                break;
                        }
                        switch (Settings.LandscapeCaptureButtonHorizontalPosition)
                        {
                            case LandscapeCaptureButtonHorizontalPosition.HomeEnd when IsViewInverted:
                                x = 0;
                                break;
                            case LandscapeCaptureButtonHorizontalPosition.HomeEnd when !IsViewInverted:
                                x = 1;
                                break;
                            case LandscapeCaptureButtonHorizontalPosition.Middle:
                                x = 0.5;
                                break;
                            case LandscapeCaptureButtonHorizontalPosition.CameraEnd when IsViewInverted:
                                x = 1;
                                break;
                            case LandscapeCaptureButtonHorizontalPosition.CameraEnd when !IsViewInverted:
                                x = 0;
                                break;
                        }
                    }
                    return new Rect(x, y, width, width);
                }

                return new Rect(0.5, 0, width, 4000);
            }
        }

        public bool IsSlidingHappening { get; set; }
        public Command SlidingStartedCommand { get; set; }
        public Command SlidingFinishedCommand { get; set; }

        public bool ShouldLineGuidesBeVisible => 
            (IsExactlyOnePictureTaken && WorkflowStage != WorkflowStage.Loading || 
             PairOperator.IsPrimary && PairOperator.PairStatus == PairStatus.Connected && 
             WorkflowStage == WorkflowStage.Capture) && Settings.AreGuideLinesVisible || 
            WorkflowStage == WorkflowStage.Keystone || WorkflowStage == WorkflowStage.ManualAlign || 
            WorkflowStage == WorkflowStage.FovCorrection;
        public bool ShouldDonutGuideBeVisible => 
            ((IsExactlyOnePictureTaken || IsNothingCaptured && Settings.IsCaptureInMirrorMode) && 
             WorkflowStage != WorkflowStage.Loading || PairOperator.IsPrimary && 
             PairOperator.PairStatus == PairStatus.Connected && WorkflowStage == WorkflowStage.Capture) && 
            Settings.IsGuideDonutVisible && Settings.Mode != DrawMode.RedCyanAnaglyph && 
            Settings.Mode != DrawMode.GrayscaleRedCyanAnaglyph && !IsFullscreenToggle;
        public bool ShouldRollGuideBeVisible => WorkflowStage == WorkflowStage.Capture && Settings.ShowRollGuide;
        public bool ShouldViewButtonBeVisible => 
            (WorkflowStage == WorkflowStage.Final || WorkflowStage == WorkflowStage.Crop ||
             WorkflowStage == WorkflowStage.Keystone || WorkflowStage == WorkflowStage.ManualAlign) &&
            !IsSlidingHappening;
        public bool ShouldClearEditButtonBeVisible => 
            (WorkflowStage == WorkflowStage.Crop || WorkflowStage == WorkflowStage.Keystone ||
             WorkflowStage == WorkflowStage.ManualAlign) && !IsSlidingHappening;
        public bool IsBusy => WorkflowStage == WorkflowStage.Loading ||
                              WorkflowStage == WorkflowStage.AutomaticAlign ||
                              WorkflowStage == WorkflowStage.Transmitting ||
                              WorkflowStage == WorkflowStage.Syncing ||
                              WorkflowStage == WorkflowStage.Saving;
        public bool IsHoldSteadySecondary { get; set; }
        public bool ShouldPortraitViewModeWarningBeVisible => IsViewPortrait && 
                                                              IsPictureWiderThanTall &&
                                                              !IsFullscreenToggle &&
                                                              WorkflowStage != WorkflowStage.Saving &&
                                                              (WorkflowStage == WorkflowStage.Final ||
                                                               WorkflowStage == WorkflowStage.Edits ||
                                                               WorkflowStage == WorkflowStage.FovCorrection) && 
                                                              Settings.Mode != DrawMode.GrayscaleRedCyanAnaglyph &&
                                                              Settings.Mode != DrawMode.RedCyanAnaglyph;

        private bool IsPictureWiderThanTall
        {
            get
            {
                if (LeftCapture != null &&
                    RightCapture != null &&
                    Settings.Mode != DrawMode.Parallel)
                {
                    var size = DrawTool.CalculateJoinedImageSizeOrientedWithEditsNoBorder(Edits, Settings, LeftCapture, LeftAlignmentTransform,
                        RightCapture, RightAlignmentTransform);
                    return size.Width > size.Height;
                }

                return false;
            }
        } 

        public string SavedSuccessMessage => (Settings.SaveToExternal
                                                 ? AppResources.Page_Camera_SavedToExternal
                                                 :
                                                 !string.IsNullOrWhiteSpace(Settings.SavingDirectory)
                                                     ?
                                                     AppResources.Page_Camera_SavedToCustomFolder
                                                     : AppResources.Page_Camera_SavedToPhotos) + "!";

        public string MovementHintText
        {
            get
            {
                if (PairOperator.IsPrimary &&
                    PairOperator.PairStatus == PairStatus.Connected)
                {
                    if (((Settings.Mode == DrawMode.Parallel ||
                          Settings.Mode == DrawMode.Cardboard) &&
                         Settings.IsCaptureLeftFirst) ||
                        Settings.Mode != DrawMode.Parallel &&
                        Settings.Mode != DrawMode.Cardboard &&
                        !Settings.IsCaptureLeftFirst)
                    {
                        return AppResources.Page_Camera_SecondaryOnRight;
                    }

                    return AppResources.Page_Camera_SecondaryOnLeft;
                }

                if (Settings.IsCaptureInMirrorMode)
                {
                    if (((Settings.Mode == DrawMode.Parallel ||
                          Settings.Mode == DrawMode.Cardboard) &&
                         Settings.IsCaptureLeftFirst) ||
                        Settings.Mode != DrawMode.Parallel &&
                        Settings.Mode != DrawMode.Cardboard &&
                        !Settings.IsCaptureLeftFirst)
                    {
                        return AppResources.Page_Camera_MirrorOnRight;
                    }

                    return AppResources.Page_Camera_MirrorOnLeft;
                }

                if (((Settings.Mode == DrawMode.Parallel ||
                      Settings.Mode == DrawMode.Cardboard) &&
                     Settings.IsCaptureLeftFirst) ||
                    Settings.Mode != DrawMode.Parallel &&
                    Settings.Mode != DrawMode.Cardboard &&
                    !Settings.IsCaptureLeftFirst)
                {
                    return AppResources.Page_Camera_MoveRight;
                }

                return AppResources.Page_Camera_MoveLeft;
            }
        }


        private WorkflowStage _stageBeforeView;
        private int _alignmentThreadLock;
        private bool _isAlignmentInvalid = true;
        private readonly IPhotoSaver _photoSaver;
        private IDeviceDisplayWrapper _deviceDisplayWrapper;
        private bool _secondaryErrorOccurred;
        private bool _isFovCorrected;

        public CameraViewModel()
        {
            _photoSaver = DependencyService.Get<IPhotoSaver>();
            _deviceDisplayWrapper = DependencyService.Get<IDeviceDisplayWrapper>();

            Settings = PersistentStorage.LoadOrDefault(PersistentStorage.SETTINGS_KEY, new Settings());
            Analytics.Initialize(Settings);
            TotalSavesCompleted = PersistentStorage.LoadOrDefault(PersistentStorage.TOTAL_SAVES_KEY, 0);
            Edits = new Edits(Settings);
            Explore = new Explore();
            LeftAlignmentTransform = SKMatrix.Identity;
            RightAlignmentTransform = SKMatrix.Identity;
            PairOperator = new PairOperator(Settings);

            CameraColumn = Settings.IsCaptureLeftFirst ? 0 : 1;
            AvailableCameras = new ObservableCollection<AvailableCamera>();

            MessagingCenter.Subscribe<App>(this, App.APP_UNPAUSING_EVENT, o => AutoconnectIfOn());

            LoadPhotoCommand = new Command(async () =>
            {
                SendCommandStartAnalyticsEvent(nameof(LoadPhotoCommand));
                var photos = await DependencyService.Get<IPhotoPicker>().GetImages();
                if (photos != null)
                {
                    LoadSharedImages(photos[0], photos[1]);
                }
            });

            RetakeLeftCommand = new Command(() =>
            {
                SendCommandStartAnalyticsEvent(nameof(RetakeLeftCommand));
                try
                {
                    if (PairOperator.PairStatus == PairStatus.Connected)
                    {
                        FullWipe();
                    }
                    else
                    {
                        if (RightCapture == null)
                        {
                            FullWipe();
                        }
                        else
                        {
                            LeftCapture = null;
                            ClearEverythingButCaptures();
                            CameraColumn = 0;
                            TryTriggerMovementHint();
                            WorkflowStage = WorkflowStage.Capture;
                        }
                    }
                }
                catch (Exception e)
                {
                    Error = e;
                }
            });

            RetakeRightCommand = new Command(() =>
            {
                SendCommandStartAnalyticsEvent(nameof(RetakeRightCommand));
                try
                {
                    if (PairOperator.PairStatus == PairStatus.Connected)
                    {
                        FullWipe();
                    }
                    else
                    {
                        if (LeftCapture == null)
                        {
                            FullWipe();
                        }
                        else
                        {
                            RightCapture = null;
                            ClearEverythingButCaptures();
                            CameraColumn = 1;
                            TryTriggerMovementHint();
                            WorkflowStage = WorkflowStage.Capture;
                        }
                    }
                }
                catch (Exception e)
                {
                    Error = e;
                }
            });

            GoToModeCommand = new Command<WorkflowStage>(arg =>
            {
                SendCommandStartAnalyticsEvent(nameof(GoToModeCommand));
                WorkflowStage = arg;
            });

            SaveEditCommand = new Command(() =>
            {
                SendCommandStartAnalyticsEvent(nameof(SaveEditCommand));
                switch (WorkflowStage)
                {
                    case WorkflowStage.Edits:
                        WorkflowStage = WorkflowStage.Final;
                        break;
                    case WorkflowStage.FovCorrection:
                        Settings.PairSettings.IsFovCorrectionSet = true;
                        PersistentStorage.Save(PersistentStorage.SETTINGS_KEY, Settings);
                        WorkflowStage = WorkflowStage.Final;
                        CheckAndCorrectResolutionFovAndAspectDifferences();
                        AutoAlign();
                        break;
                    default:
                        WorkflowStage = WorkflowStage.Edits;
                        break;
                }
            });

            ClearEditCommand = new Command(() =>
            {
                SendCommandStartAnalyticsEvent(nameof(ClearEditCommand));
                try
                {
                    switch (WorkflowStage)
                    {
                        case WorkflowStage.Crop:
                            ClearCrops();
                            break;
                        case WorkflowStage.ManualAlign:
                            ClearManualAlignments();
                            break;
                        case WorkflowStage.Keystone:
                            ClearKeystone();
                            break;
                    }
                }
                catch (Exception e)
                {
                    Error = e;
                }
            });

            ClearCapturesCommand = new Command(async() =>
            {
                SendCommandStartAnalyticsEvent(nameof(ClearCapturesCommand));
                try
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        if (!_isClearPromptOpen)
                        {
                            _isClearPromptOpen = true;
                            var confirmClear = await CoreMethods.DisplayAlert(AppResources.Page_Camera_ReallyClear,
                                AppResources.Page_Camera_SureClear, AppResources.Button_YesClear, AppResources.Button_No);
                            if (confirmClear)
                            {
                                FullWipe();
                            }
                            _isClearPromptOpen = false;
                        }
                    });
                }
                catch (Exception e)
                {
                    Error = e;
                }
            });

            CapturePictureCommand = new Command(() =>
            {
                SendCommandStartAnalyticsEvent(nameof(CapturePictureCommand));
                if (PairOperator.IsPrimary &&
                    PairOperator.PairStatus == PairStatus.Connected)
                {
                    PairOperator.BeginSyncedCapture();
                }
                else
                {
                    CapturePictureTrigger = !CapturePictureTrigger;
                }
            });

            ToggleViewModeCommand = new Command(() =>
            {
                SendCommandStartAnalyticsEvent(nameof(ToggleViewModeCommand));
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
                SendCommandStartAnalyticsEvent(nameof(NavigateToSettingsCommand));
                await CoreMethods.PushPageModel<SettingsViewModel>(Settings);
            });

            NavigateToHamburgerPageCommand = new Command(async () =>
            {
                SendCommandStartAnalyticsEvent(nameof(NavigateToHamburgerPageCommand));
                await CoreMethods.PushPageModel<HamburgerViewModel>(Settings);
            });

            FlipCameraCommand = new Command(() =>
            {
                SendCommandStartAnalyticsEvent(nameof(FlipCameraCommand));
                var index = AvailableCameras.IndexOf(ChosenCamera);
                index++;
                if (index > AvailableCameras.Count - 1)
                {
                    index = 0;
                }

                ChosenCamera = AvailableCameras.ElementAt(index);
            });

            OpenCameraSettingsCommand = new Command(() =>
            {
                SendCommandStartAnalyticsEvent(nameof(OpenCameraSettingsCommand));
                CameraSettingsVisible = !CameraSettingsVisible;
                if (!CameraSettingsVisible)
                {
                    CameraSettingMode = CameraSettingMode.Menu;
                }
            });

            SetCameraSettingModeCommand = new Command<CameraSettingMode>(mode =>
            {
                SendCommandStartAnalyticsEvent(nameof(SetCameraSettingModeCommand));
                CameraSettingMode = mode;
            });

            SaveCameraSettingCommand = new Command(() =>
            {
                SendCommandStartAnalyticsEvent(nameof(SaveCameraSettingCommand));
                if (CameraSettingMode == CameraSettingMode.Menu)
                {
                    CameraSettingsVisible = false;
                }
                else
                {
                    CameraSettingMode = CameraSettingMode.Menu;
                }
            });

            ResetCameraSettingCommand = new Command(() =>
            {
                SendCommandStartAnalyticsEvent(nameof(ResetCameraSettingCommand));
                switch (CameraSettingMode)
                {
                    case CameraSettingMode.Camera:
                        ChosenCamera = AvailableCameras.FirstOrDefault(c => !c.IsFront);
                        break;
                    case CameraSettingMode.ISO:
                        break;
                    case CameraSettingMode.Exposure:
                        break;
                    case CameraSettingMode.FrameDuration:
                        break;
                    case CameraSettingMode.WhiteBalance:
                        break;
                    case CameraSettingMode.Flash:
                        break;
                    case CameraSettingMode.Menu:
                        ChosenCamera = AvailableCameras.FirstOrDefault(c => !c.IsFront);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            });

            SwapSidesCommand = new Command(obj =>
            {
                SendCommandStartAnalyticsEvent(nameof(SwapSidesCommand));
                if (WorkflowStage == WorkflowStage.Capture ||
                    WorkflowStage == WorkflowStage.Final ||
                    WorkflowStage == WorkflowStage.Edits ||
                    obj is bool forced && forced)
                {
                    (LeftCapture, RightCapture) = (RightCapture, LeftCapture);
                    (LeftAlignmentTransform, RightAlignmentTransform) = (RightAlignmentTransform, LeftAlignmentTransform);

                    if (WorkflowStage == WorkflowStage.Capture)
                    {
                        CameraColumn = CameraColumn == 0 ? 1 : 0;
                    }

                    if (WorkflowStage != WorkflowStage.Capture)
                    {
                        (Edits.InsideCrop, Edits.OutsideCrop) = (Edits.OutsideCrop, Edits.InsideCrop);
                        (Edits.LeftRotation, Edits.RightRotation) = (Edits.RightRotation, Edits.LeftRotation);
                        (Edits.LeftZoom, Edits.RightZoom) = (Edits.RightZoom, Edits.LeftZoom);
                        Edits.VerticalAlignment = -Edits.VerticalAlignment;
                    }

                    Settings.IsCaptureLeftFirst = !Settings.IsCaptureLeftFirst;
                    PersistentStorage.Save(PersistentStorage.SETTINGS_KEY, Settings);

                    TryTriggerMovementHint();
                }
            });

            SetCropMode = new Command(mode =>
            {
                SendCommandStartAnalyticsEvent(nameof(SetCropMode));
                CropMode = (CropMode) mode;
            });

            SetManualAlignMode = new Command(mode =>
            {
                SendCommandStartAnalyticsEvent(nameof(SetManualAlignMode));
                ManualAlignMode = (ManualAlignMode) mode;
            });

            SetFovCorrectionMode = new Command(mode =>
            {
                SendCommandStartAnalyticsEvent(nameof(SetFovCorrectionMode));
                FovCorrectionMode = (FovCorrectionMode) mode;
            });

            SlidingStartedCommand = new Command(() =>
            {
                IsSlidingHappening = true;
            });

            SlidingFinishedCommand = new Command(() =>
            {
                IsSlidingHappening = false;
            });

            SaveCapturesCommand = new Command(async () =>
            {
                SendCommandStartAnalyticsEvent(nameof(SaveCapturesCommand));

                if (!Settings.AnySaveModesOn)
                {
                    await CoreMethods.DisplayAlert(AppResources.Page_Camera_NoSaveModes,
                        AppResources.Page_Camera_NoSaveModesExplain, AppResources.Button_OK);
                    return;
                }

                const string SAVE_EVENT = "image_saved";
                const string SAVE_TYPE = "type";
                WorkflowStage = WorkflowStage.Saving;

                try
                {
                    await Task.Run(async () =>
                    {
                        if (Settings.SaveSidesSeparately)
                        {
                            Analytics.TrackEvent(SAVE_EVENT, new Dictionary<string, object>
                            {
                                {SAVE_TYPE, "separate_sides"}
                            });
                            var leftWidth = LeftCapture.Width;
                            var leftHeight = LeftCapture.Height;

                            using var tempSurface =
                                SKSurface.Create(new SKImageInfo(leftWidth, leftHeight));
                            using var canvas = tempSurface.Canvas;

                            canvas.DrawBitmap(LeftCapture, 0, 0);

                            await SaveSurfaceSnapshot(tempSurface, AppResources.SaveModes_Separate);

                            canvas.Clear();

                            canvas.DrawBitmap(RightCapture, 0, 0);

                            await SaveSurfaceSnapshot(tempSurface, AppResources.SaveModes_Separate);
                        }

                        var joinedImageSize = DrawTool.CalculateJoinedImageSizeOrientedWithEditsNoBorder(Edits, Settings,
                            LeftCapture, LeftAlignmentTransform, RightCapture, RightAlignmentTransform);

                        var tripleWidth = joinedImageSize.Width * 1.5f;
                        var quadHeight = joinedImageSize.Height * 2f;
                        var quadOffset = joinedImageSize.Height;

                        var borderThickness = Settings.AddBorder2
                            ? DrawTool.CalculateBorderThickness(joinedImageSize.Width / 2f, Settings.BorderWidthProportion)
                            : 0;
                        joinedImageSize.Width += 3 * borderThickness;
                        joinedImageSize.Height += 2 * borderThickness;
                        var tripleOffset = joinedImageSize.Width - borderThickness;
                        tripleWidth += 4 * borderThickness;
                        

                        var fuseGuideMarginHeight = Settings.SaveWithFuseGuide
                            ? DrawTool.CalculateFuseGuideMarginHeight(joinedImageSize.Height)
                            : 0;
                        var fuseGuideImageHeightModifier = Math.Max(fuseGuideMarginHeight - borderThickness, 0);

                        joinedImageSize.Height += fuseGuideImageHeightModifier;
                        quadHeight += 2 * fuseGuideImageHeightModifier + 2 * borderThickness;
                        quadOffset += fuseGuideImageHeightModifier + borderThickness;

                        tripleOffset *= Settings.ResolutionProportion / 100f;
                        tripleWidth *= Settings.ResolutionProportion / 100f;
                        joinedImageSize.Width *= Settings.ResolutionProportion / 100f;
                        joinedImageSize.Height *= Settings.ResolutionProportion / 100f;
                        quadHeight *= Settings.ResolutionProportion / 100f;
                        quadOffset *= Settings.ResolutionProportion / 100f;

                        var joinedImageSkInfo =
                            new SKImageInfo((int) joinedImageSize.Width, (int) joinedImageSize.Height);

                        if (Settings.SaveForCrossView &&
                            Settings.Mode == DrawMode.Cross ||
                            Settings.SaveForParallel &&
                            Settings.Mode == DrawMode.Parallel ||
                            Settings.SaveForCrossView && 
                            Settings.Mode == DrawMode.RedCyanAnaglyph ||
                            Settings.SaveForCrossView &&
                            Settings.Mode == DrawMode.GrayscaleRedCyanAnaglyph)
                        {
                            Analytics.TrackEvent(SAVE_EVENT, new Dictionary<string, object>
                            {
                                {SAVE_TYPE, Settings.Mode == DrawMode.Parallel ? "parallel" : "cross"}
                            });
                            using var tempSurface = SKSurface.Create(joinedImageSkInfo);
                            
                            DrawTool.DrawImagesOnCanvas(
                                tempSurface, 
                                LeftCapture, LeftAlignmentTransform,
                                RightCapture, RightAlignmentTransform,
                                Settings,
                                Edits, 
                                DrawMode.Cross, WasCapturePaired);

                            await SaveSurfaceSnapshot(tempSurface, Settings.Mode == DrawMode.Parallel ? AppResources.SaveModes_Parallel : AppResources.SaveModes_Cross);
                        }

                        if (Settings.SaveForParallel &&
                            Settings.Mode == DrawMode.Cross ||
                            Settings.SaveForCrossView &&
                            Settings.Mode == DrawMode.Parallel)
                        {
                            Analytics.TrackEvent(SAVE_EVENT, new Dictionary<string, object>
                            {
                                {SAVE_TYPE, Settings.Mode == DrawMode.Cross ? "parallel" : "cross"}
                            });
                            using var tempSurface =
                                SKSurface.Create(joinedImageSkInfo);

                            DrawTool.DrawImagesOnCanvas(
                                tempSurface, 
                                LeftCapture, LeftAlignmentTransform,
                                RightCapture, RightAlignmentTransform,
                                Settings, 
                                Edits, 
                                DrawMode.Parallel, WasCapturePaired, withSwap: true);

                            await SaveSurfaceSnapshot(tempSurface, Settings.Mode == DrawMode.Cross ? AppResources.SaveModes_Parallel : AppResources.SaveModes_Cross);
                        }

                        if (Settings.SaveForRedCyanAnaglyph)
                        {
                            Analytics.TrackEvent(SAVE_EVENT, new Dictionary<string, object>
                            {
                                {SAVE_TYPE, "red_cyan_anaglyph"}
                            });
                            await DrawAnaglyph(false);
                        }

                        if (Settings.SaveForGrayscaleAnaglyph)
                        {
                            Analytics.TrackEvent(SAVE_EVENT, new Dictionary<string, object>
                            {
                                {SAVE_TYPE, "grayscale_anaglyph"}
                            });
                            await DrawAnaglyph(true);
                        }

                        if (Settings.SaveRedundantFirstSide)
                        {
                            Analytics.TrackEvent(SAVE_EVENT, new Dictionary<string, object>
                            {
                                {SAVE_TYPE, "first_side"}
                            });
                            var targetBitmap = Settings.IsCaptureLeftFirst ? LeftCapture : RightCapture;

                            var width = targetBitmap.Width;
                            var height = targetBitmap.Height;

                            using var tempSurface =
                                SKSurface.Create(new SKImageInfo(width, height));
                            using var canvas = tempSurface.Canvas;

                            canvas.DrawBitmap(targetBitmap, 0, 0);

                            await SaveSurfaceSnapshot(tempSurface, AppResources.SaveModes_Single);
                        }

                        if (Settings.SaveForTriple)
                        {
                            Analytics.TrackEvent(SAVE_EVENT, new Dictionary<string, object>
                            {
                                {SAVE_TYPE, "triple"}
                            });
                            using var doubleSurface =
                                SKSurface.Create(joinedImageSkInfo);
                            using var doubleCanvas = doubleSurface.Canvas;

                            DrawTool.DrawImagesOnCanvas(
                                doubleSurface, 
                                LeftCapture, LeftAlignmentTransform,
                                RightCapture, RightAlignmentTransform,
                                Settings,
                                Edits,
                                DrawMode.Cross, WasCapturePaired);

                            using var tripleSurface =
                                SKSurface.Create(new SKImageInfo((int)tripleWidth, (int)joinedImageSize.Height));
                            using var tripleCanvas = tripleSurface.Canvas;
                            tripleCanvas.Clear();

                            tripleCanvas.DrawSurface(doubleSurface, 0, 0);
                            tripleCanvas.DrawSurface(doubleSurface, tripleOffset, 0);

                            await SaveSurfaceSnapshot(tripleSurface, AppResources.SaveModes_Triple);
                        }

                        if (Settings.SaveForQuad)
                        {
                            Analytics.TrackEvent(SAVE_EVENT, new Dictionary<string, object>
                            {
                                {SAVE_TYPE, "quad"}
                            });
                            using var doublePlainSurface =
                                SKSurface.Create(joinedImageSkInfo);
                            using var doublePlainCanvas = doublePlainSurface.Canvas;

                            DrawTool.DrawImagesOnCanvas(
                                doublePlainSurface, 
                                LeftCapture, LeftAlignmentTransform,
                                RightCapture, RightAlignmentTransform,
                                Settings,
                                Edits,
                                DrawMode.Cross, WasCapturePaired);

                            using var doubleSwapSurface =
                                SKSurface.Create(joinedImageSkInfo);
                            using var doubleSwapCanvas = doubleSwapSurface.Canvas;
                            doubleSwapCanvas.Clear();

                            DrawTool.DrawImagesOnCanvas(
                                doubleSwapSurface, 
                                LeftCapture, LeftAlignmentTransform,
                                RightCapture, RightAlignmentTransform,
                                Settings,
                                Edits,
                                DrawMode.Cross, WasCapturePaired, withSwap: true);

                            using var quadSurface =
                                SKSurface.Create(new SKImageInfo((int)joinedImageSize.Width, (int)quadHeight));
                            using var quadCanvas = quadSurface.Canvas;
                            quadCanvas.Clear();

                            quadCanvas.DrawSurface(doublePlainSurface, 0, 0);
                            quadCanvas.DrawSurface(doubleSwapSurface, 0, (int)quadOffset);

                            await SaveSurfaceSnapshot(quadSurface, AppResources.SaveModes_Quad);
                        }

                        if (Settings.SaveForCardboard)
                        {
                            Analytics.TrackEvent(SAVE_EVENT, new Dictionary<string, object>
                            {
                                {SAVE_TYPE, "cardboard"}
                            });
                            var finalSize = DrawTool.CalculateJoinedImageSizeOrientedWithEditsNoBorder(Edits, Settings,
                                LeftCapture, LeftAlignmentTransform, RightCapture, RightAlignmentTransform);

                            using var tempSurface = SKSurface.Create(new SKImageInfo((int)finalSize.Width, (int)finalSize.Height));
                            using var canvas = tempSurface.Canvas;
                            canvas.Clear();

                            var withBorderTemp = Settings.AddBorder2;
                            Settings.AddBorder2 = false;
                            var fuseGuideTemp = Settings.SaveWithFuseGuide;
                            Settings.SaveWithFuseGuide = false;

                            DrawTool.DrawImagesOnCanvas(tempSurface, 
                                LeftCapture, LeftAlignmentTransform,
                                RightCapture, RightAlignmentTransform,
                                Settings, Edits, DrawMode.Parallel, WasCapturePaired, withSwap: Settings.Mode == DrawMode.Cross ||
                                Settings.Mode == DrawMode.RedCyanAnaglyph ||
                                Settings.Mode == DrawMode.GrayscaleRedCyanAnaglyph);

                            Settings.AddBorder2 = withBorderTemp;
                            Settings.SaveWithFuseGuide = fuseGuideTemp;

                            await SaveSurfaceSnapshot(tempSurface, AppResources.SaveModes_Cardboard);
                        }

                        TotalSavesCompleted++;
                        PersistentStorage.Save(PersistentStorage.TOTAL_SAVES_KEY, TotalSavesCompleted);
                        Analytics.TrackEvent("photo save",new Dictionary<string, object>
                        {
                            { "total count", TotalSavesCompleted}
                        });
                    });
                }
                catch (DirectoryNotFoundException)
                {
                    SaveFailFadeTrigger = !SaveFailFadeTrigger;
                    WorkflowStage = WorkflowStage.Final;

                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await CoreMethods.DisplayAlert(AppResources.Page_Camera_DirectoryNotFound,
                            AppResources.Page_Camera_DirectoryNotFoundExplain,
                            AppResources.Button_OK);
                    });

                    return;
                }
                catch (Exception e)
                {
                    SaveFailFadeTrigger = !SaveFailFadeTrigger;
                    Error = e;
                    WorkflowStage = WorkflowStage.Final;

                    return;
                }
                
                SaveSuccessFadeTrigger = !SaveSuccessFadeTrigger;
                if (Settings.ClearCapturesAfterSave)
                {
                    FullWipe();
                }
                else
                {
                    WorkflowStage = WorkflowStage.Final;
                }
#if !DEBUG
                if (TotalSavesCompleted % 10 == 0 &&
                    TotalSavesCompleted > 0)
                {
                    DependencyService.Get<IStoreReviewOpener>()?.TryOpenStoreReview();
                }
#endif
            });

            PromptForPermissionAndSendErrorEmailCommand = new Command(async () =>
            {
                SendCommandStartAnalyticsEvent(nameof(PromptForPermissionAndSendErrorEmailCommand));
                Debug.WriteLine("### ERROR: " + Error);

                var deviceInfoDictionary = new Dictionary<string, object>
                {
                    {"Platform", DeviceInfo.Platform.ToString()},
                    {"Manufacturer", DeviceInfo.Manufacturer},
                    {"Model", DeviceInfo.Model},
                    {"Width", Math.Round(Application.Current.MainPage.Width).ToString()},
                    {"Height", Math.Round(Application.Current.MainPage.Height).ToString()},
                    {"vNumber", DeviceInfo.Version.ToString()},
                    {"vString", DeviceInfo.VersionString},
                    {"App Version", AppInfo.VersionString},
                    {"Idiom", DeviceInfo.Current.Idiom.ToString()}
                };

                Analytics.Crashes.TrackError(Error, deviceInfoDictionary);

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
#if DEBUG
                    await CoreMethods.DisplayAlert("ERROR", Error.ToString(), "OK");
#else
                    if (Settings.PromptForErrorEmails)
                    {
                        var sendReport = await CoreMethods.DisplayAlert(AppResources.Page_Camera_Oops,
                            AppResources.Page_Camera_SorryError,
                            AppResources.Button_EmailMeNow, AppResources.Button_DoNotEmailMe);
                        if (sendReport)
                        {
                            OpenLink.Execute(
                                "mailto:me@kra2008.com?subject=CrossCam%20error%20report&body=" +
                                AppResources.Page_Camera_ErrorEmailBody +
                                HttpUtility.UrlEncode(Error.ToString()));
                        }
                        else
                        {
                            Analytics.TrackEvent("declined to send error report");
                        }
                    }
                    else
                    {
                        Analytics.TrackEvent("error, but error reports turned off");
                    }
#endif
                });
                Error = null;
            });

            PairCommand = new Command(async () =>
            {
                SendCommandStartAnalyticsEvent(nameof(PairCommand));
                try
                {
                    if (PairOperator.PairStatus == PairStatus.Disconnected)
                    {
                        if (!Settings.PairSettings.IsPairedPrimary.HasValue)
                        {
                            await MainThread.InvokeOnMainThreadAsync(async () =>
                            {
                                await CoreMethods.DisplayAlert(AppResources.Page_Camera_PairRoleNotSelected,
                                    AppResources.Page_Camera_GoPickRole,
                                    AppResources.Button_OK);
                            });
                        }
                        else
                        {
                            if (Settings.PairSettings.IsPairedPrimary.Value)
                            {
                                Analytics.TrackEvent("attempt primary pairing");
                                await PairOperator.SetUpPrimaryForPairing();
                            }
                            else
                            {
                                await PairOperator.SetUpSecondaryForPairing();
                            }
                        }
                    }
                    else
                    {
                        PairOperator.Disconnect();
                    }
                }
                catch (Exception e)
                {
                    Error = e;
                }
            });

            ToggleFullscreen = new Command(() =>
            {
                IsFullscreenToggle = !IsFullscreenToggle;
            });

            ToggleAutoalignCommand = new Command(() =>
            {
                Settings.AlignmentSettings.IsAutomaticAlignmentOn = !Settings.AlignmentSettings.IsAutomaticAlignmentOn;
                PersistentStorage.Save(PersistentStorage.SETTINGS_KEY, Settings);
                AutoAlign();
            });
        }

        public override void Init(object initData)
        {
            base.Init(initData);

            PropertyChanged += HandlePropertyChanged;
            Settings.PropertyChanged += SettingsOnPropertyChanged;
            Settings.AlignmentSettings.PropertyChanged += AlignmentSettingsOnPropertyChanged;
            Settings.PairSettings.PropertyChanged += PairSettingsOnPropertyChanged;
            Settings.EditsSettings.PropertyChanged += EditsSettingsOnPropertyChanged;
            PairOperator.PropertyChanged += PairOperatorOnPropertyChanged;
            PairOperator.Connected += PairOperatorOnConnected;
            PairOperator.Disconnected += PairOperatorOnDisconnected;
            PairOperator.PreviewFrameReceived += PairOperatorOnPreviewFrameReceived;
            PairOperator.PreviewFrameRequestReceived += PairOperatorOnPreviewFrameRequestReceived;
            PairOperator.CapturedImageReceived += PairOperatorOnCapturedImageReceived;
            PairOperator.SyncRequested += PairOperatorOnSyncRequested;
            PairOperator.InitialSyncStarted += PairOperatorInitialSyncStarted;
            PairOperator.InitialSyncCompleted += PairOperatorInitialSyncCompleted;
            PairOperator.TransmissionStarted += PairOperatorTransmissionStarted;
            PairOperator.TransmissionComplete += PairOperatorTransmissionComplete;
            PairOperator.CountdownDisplayTimerCompleteSecondary += PairOperatorCountdownDisplayTimerCompleteSecondary;
            PairOperator.ErrorOccurred += PairOperatorOnErrorOccurred;
            PairOperator.TimeoutOccurred += PairOperatorTimeoutOccurred;

            _deviceDisplayWrapper.DisplayInfoChanged += DeviceDisplayOnMainDisplayInfoChanged;

            Analytics.TrackEvent("settings_at_launch", new Dictionary<string, object>
            {
                {"settings",Settings}
            });

            //var settingsDictionary = JsonConvert
            //    .DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(Settings))
            //    .ToDictionary(pair => pair.Key, pair => pair.Value);
            //Analytics.TrackEvent("settings_at_launch", settingsDictionary);
            //var alignmentDictionary = JsonConvert
            //    .DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(Settings.AlignmentSettings))
            //    .ToDictionary(pair => pair.Key, pair => pair.Value);
            //Analytics.TrackEvent("alignment_settings_at_launch", alignmentDictionary);
        }

        private DisplayOrientation _previousOrientation = DisplayOrientation.Unknown;
        private void DeviceDisplayOnMainDisplayInfoChanged(object sender, DisplayInfoChangedEventArgs e)
        {
            DisplayOrientation = e.DisplayInfo.Orientation;
            DisplayRotation = e.DisplayInfo.Rotation;

            if (Settings.Mode == DrawMode.Cardboard)
            {
                switch (WorkflowStage)
                {
                    case WorkflowStage.Capture when !IsViewPortrait && _previousOrientation == DisplayOrientation.Portrait:
                        SwapSidesCommand.Execute(null);
                        break;
                    case WorkflowStage.Final:
                        ClearCapturesCommand.Execute(null);
                        break;
                }
            }

            RaisePropertyChanged(nameof(IsViewPortrait));
            RaisePropertyChanged(nameof(IsViewInverted));

            _previousOrientation = DisplayOrientation;
        }

        private static void SendCommandStartAnalyticsEvent(string name)
        {
            Analytics.DropBreadcrumb(COMMAND_ANALYTICS_EVENT, new Dictionary<string, object>
            {
                {COMMAND_ANALYTICS_KEY_NAME, name}
            });
        }

        private void HandlePropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            switch (args.PropertyName)
            {
                case nameof(CaptureSuccess):
                    CaptureSuccessTrigger = !CaptureSuccessTrigger;
                    break;
                case nameof(LocalCapturedFrame) when LocalCapturedFrame == null:
                    return;
                case nameof(LocalCapturedFrame) when _secondaryErrorOccurred:
                    FullWipe();
                    break;
                case nameof(LocalCapturedFrame):
                    if (PairOperator.IsPrimary &&
                        PairOperator.PairStatus == PairStatus.Connected)
                    {
                        WasCapturePaired = true;
                        if (LeftCapture == null && RightCapture == null)
                        {
                            WorkflowStage = WorkflowStage.Loading;
                        }
                        RaisePropertyChanged(nameof(ShouldLineGuidesBeVisible));
                        RaisePropertyChanged(nameof(ShouldDonutGuideBeVisible));
                    }
                    else
                    {
                        WasCapturePaired = false;
                    }

                    if (Settings.IsCaptureInMirrorMode)
                    {
                        if (Settings.IsCaptureLeftFirst)
                        {
                            SetLeftBitmap(GetHalfOfImage(LocalCapturedFrame.Frame, IsParallelTypeMode, false, LocalCapturedFrame.Orientation,isFrontFacing:LocalCapturedFrame.IsFrontFacing), true, true);
                            SetRightBitmap(GetHalfOfImage(LocalCapturedFrame.Frame, !IsParallelTypeMode, false, LocalCapturedFrame.Orientation, true, LocalCapturedFrame.IsFrontFacing), true, true);
                        }
                        else
                        {
                            SetLeftBitmap(GetHalfOfImage(LocalCapturedFrame.Frame, IsParallelTypeMode, false, LocalCapturedFrame.Orientation, true, LocalCapturedFrame.IsFrontFacing), true, true);
                            SetRightBitmap(GetHalfOfImage(LocalCapturedFrame.Frame, !IsParallelTypeMode, false, LocalCapturedFrame.Orientation, isFrontFacing: LocalCapturedFrame.IsFrontFacing), true, true);
                        }
                    }
                    else if (PairOperator.PairStatus != PairStatus.Connected)
                    {
                        if (CameraColumn == 0)
                        {
                            SetLeftBitmap(AutoOrient(LocalCapturedFrame.Frame, LocalCapturedFrame.Orientation, LocalCapturedFrame.IsFrontFacing), true, true);
                        }
                        else
                        {
                            SetRightBitmap(AutoOrient(LocalCapturedFrame.Frame, LocalCapturedFrame.Orientation, LocalCapturedFrame.IsFrontFacing), true, true);
                        }
                    } 
                    else if (PairOperator.PairStatus == PairStatus.Connected)
                    {
                        Debug.WriteLine("### we have a locally captured frame");
                        if (Settings.IsCaptureLeftFirst)
                        {
                            SetLeftBitmap(AutoOrient(LocalCapturedFrame.Frame, LocalCapturedFrame.Orientation, LocalCapturedFrame.IsFrontFacing), false, true);
                        }
                        else
                        {
                            SetRightBitmap(AutoOrient(LocalCapturedFrame.Frame, LocalCapturedFrame.Orientation, LocalCapturedFrame.IsFrontFacing), false, true);
                        }
                    }

                    LocalCapturedFrame = null;
                    
                    break;
                case nameof(Error):
                    if (Error != null)
                    {
                        PromptForPermissionAndSendErrorEmailCommand.Execute(null);
                    }

                    break;
                case nameof(WasSwipedTrigger):
                    SwapSidesCommand?.Execute(null);
                    break;
                case nameof(IsFullscreenToggle):
                    TryTriggerMovementHint(true);
                    RaisePropertyChanged(nameof(CanvasRectangle));
                    RaisePropertyChanged(nameof(CanvasRectangleFlags));
                    RaisePropertyChanged(nameof(ShouldDonutGuideBeVisible));
                    break;
                case nameof(IsNothingCaptured):
                    RaisePropertyChanged(nameof(IsFullscreenToggleVisible));
                    RaisePropertyChanged(nameof(UseFullScreenWidth));
                    RaisePropertyChanged(nameof(ShouldPairButtonBeVisible));
                    RaisePropertyChanged(nameof(ShouldLineGuidesBeVisible));
                    RaisePropertyChanged(nameof(ShouldDonutGuideBeVisible));
                    RaisePropertyChanged(nameof(ShouldPortraitViewModeWarningBeVisible));
                    break;
                case nameof(IsViewPortrait):
                    RaisePropertyChanged(nameof(CaptureButtonPosition));
                    RaisePropertyChanged(nameof(CanvasRectangle));
                    RaisePropertyChanged(nameof(ShouldLeftLeftRetakeBeVisible));
                    RaisePropertyChanged(nameof(ShouldRightRightRetakeBeVisible));
                    RaisePropertyChanged(nameof(ShouldPortraitViewModeWarningBeVisible));
                    TryTriggerMovementHint();
                    break;
                case nameof(IsViewInverted):
                    RaisePropertyChanged(nameof(CaptureButtonPosition));
                    break;
                case nameof(WorkflowStage):
                    RaisePropertyChanged(nameof(ShouldPairButtonBeVisible));
                    RaisePropertyChanged(nameof(ShouldRollGuideBeVisible));
                    RaisePropertyChanged(nameof(UseFullScreenWidth));
                    RaisePropertyChanged(nameof(CanvasRectangle));
                    RaisePropertyChanged(nameof(IsFullscreenToggleVisible));
                    RaisePropertyChanged(nameof(IsFullscreenToggle));
                    RaisePropertyChanged(nameof(ShouldLeftLeftRetakeBeVisible));
                    RaisePropertyChanged(nameof(ShouldLeftRightRetakeBeVisible));
                    RaisePropertyChanged(nameof(ShouldRightLeftRetakeBeVisible));
                    RaisePropertyChanged(nameof(ShouldRightRightRetakeBeVisible));
                    RaisePropertyChanged(nameof(ShouldCenterLoadBeVisible));
                    RaisePropertyChanged(nameof(ShouldLeftLoadBeVisible));
                    RaisePropertyChanged(nameof(ShouldRightLoadBeVisible));
                    RaisePropertyChanged(nameof(ShouldSwapSidesBeVisible));
                    RaisePropertyChanged(nameof(ShouldSettingsAndHelpBeVisible));
                    RaisePropertyChanged(nameof(ShouldCaptureButtonBeVisible));
                    RaisePropertyChanged(nameof(ShouldLineGuidesBeVisible));
                    RaisePropertyChanged(nameof(ShouldDonutGuideBeVisible));
                    RaisePropertyChanged(nameof(ShouldViewButtonBeVisible));
                    RaisePropertyChanged(nameof(ShouldClearEditButtonBeVisible));
                    RaisePropertyChanged(nameof(IsBusy));
                    RaisePropertyChanged(nameof(ShouldPortraitViewModeWarningBeVisible));
                    if (WorkflowStage == WorkflowStage.View) ExploreReminderTrigger = !ExploreReminderTrigger;
                    break;
                case nameof(IsExactlyOnePictureTaken):
                    RaisePropertyChanged(nameof(ShouldSwapSidesBeVisible));
                    RaisePropertyChanged(nameof(ShouldDonutGuideBeVisible));
                    RaisePropertyChanged(nameof(ShouldLineGuidesBeVisible));
                    RaisePropertyChanged(nameof(WorkflowStage));
                    break;
                case nameof(CaptureButtonPosition):
                    RaisePropertyChanged(nameof(PairButtonPosition));
                    break;
                case nameof(UseFullScreenWidth):
                    RaisePropertyChanged(nameof(CanvasRectangle));
                    RaisePropertyChanged(nameof(CanvasRectangleFlags));
                    break;
                case nameof(IsSlidingHappening):
                    RaisePropertyChanged(nameof(ShouldViewButtonBeVisible));
                    RaisePropertyChanged(nameof(ShouldClearEditButtonBeVisible));
                    break;
                case nameof(LeftCapture):
                    RaisePropertyChanged(nameof(IsNothingCaptured));
                    RaisePropertyChanged(nameof(AreBothSidesCaptured));
                    RaisePropertyChanged(nameof(ShouldLeftLeftRetakeBeVisible));
                    RaisePropertyChanged(nameof(ShouldLeftRightRetakeBeVisible));
                    RaisePropertyChanged(nameof(IsExactlyOnePictureTaken));
                    break;
                case nameof(RightCapture):
                    RaisePropertyChanged(nameof(IsNothingCaptured));
                    RaisePropertyChanged(nameof(AreBothSidesCaptured));
                    RaisePropertyChanged(nameof(ShouldRightLeftRetakeBeVisible));
                    RaisePropertyChanged(nameof(ShouldRightRightRetakeBeVisible));
                    RaisePropertyChanged(nameof(IsExactlyOnePictureTaken));
                    break;
                case nameof(WasCapturePortrait):
                    RaisePropertyChanged(nameof(ShouldLeftLeftRetakeBeVisible));
                    RaisePropertyChanged(nameof(ShouldRightRightRetakeBeVisible));
                    break;
                case nameof(IsBusy):
                    RaisePropertyChanged(nameof(ShouldSettingsAndHelpBeVisible));
                    break;
            }
        }

        private void SettingsOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Settings.Mode):
                case nameof(Settings.SaveForRedCyanAnaglyph):
                case nameof(Settings.SaveForCrossView):
                case nameof(Settings.SaveForGrayscaleAnaglyph):
                case nameof(Settings.SaveForParallel):
                    _isAlignmentInvalid = true;
                    goto case nameof(Settings.SaveForCardboard);
                case nameof(Settings.SaveForCardboard):
                case nameof(Settings.SaveForQuad):
                case nameof(Settings.SaveForTriple):
                case nameof(Settings.SaveRedundantFirstSide):
                case nameof(Settings.SaveSidesSeparately):
                    RaisePropertyChanged(nameof(WorkflowStage));
                    break;
                case nameof(Settings.AreGuideLinesVisible):
                case nameof(Settings.AreGuideLinesColored):
                    RaisePropertyChanged(nameof(ShouldLineGuidesBeVisible));
                    break;
                case nameof(Settings.ShowRollGuide):
                    RaisePropertyChanged(nameof(ShouldRollGuideBeVisible));
                    break;
                case nameof(Settings.IsGuideDonutVisible):
                    RaisePropertyChanged(nameof(ShouldDonutGuideBeVisible));
                    break;
                case nameof(Settings.PortraitCaptureButtonPosition):
                    RaisePropertyChanged(nameof(ShouldLeftLeftRetakeBeVisible));
                    RaisePropertyChanged(nameof(ShouldLeftRightRetakeBeVisible));
                    RaisePropertyChanged(nameof(ShouldRightLeftRetakeBeVisible));
                    RaisePropertyChanged(nameof(ShouldRightRightRetakeBeVisible));
                    RaisePropertyChanged(nameof(ShouldLeftLoadBeVisible));
                    RaisePropertyChanged(nameof(ShouldCenterLoadBeVisible));
                    RaisePropertyChanged(nameof(ShouldRightLoadBeVisible));
                    RaisePropertyChanged(nameof(CaptureButtonPosition));
                    break;
                case nameof(Settings.LandscapeCaptureButtonHorizontalPosition):
                case nameof(Settings.LandscapeCaptureButtonVerticalPosition):
                    RaisePropertyChanged(nameof(CaptureButtonPosition));
                    break;
                case nameof(Settings.IsCaptureInMirrorMode):
                    RaisePropertyChanged(nameof(IsFullscreenToggleVisible));
                    RaisePropertyChanged(nameof(ShouldPairButtonBeVisible));
                    RaisePropertyChanged(nameof(ShouldSwapSidesBeVisible));
                    RaisePropertyChanged(nameof(ShouldDonutGuideBeVisible));
                    RaisePropertyChanged(nameof(UseFullScreenWidth));
                    RaisePropertyChanged(nameof(ShouldCenterLoadBeVisible));
                    RaisePropertyChanged(nameof(ShouldRightLoadBeVisible));
                    RaisePropertyChanged(nameof(ShouldLeftLoadBeVisible));
                    RaisePropertyChanged(nameof(MovementHintText));
                    break;
                case nameof(Settings.IsCaptureLeftFirst):
                    RaisePropertyChanged(nameof(PairButtonPosition));
                    RaisePropertyChanged(nameof(MovementHintText));
                    break;
                case nameof(Settings.PairButtonHorizontalPosition):
                    RaisePropertyChanged(nameof(PairButtonPosition));
                    break;
                case nameof(Settings.SaveToExternal):
                case nameof(Settings.SavingDirectory):
                    RaisePropertyChanged(nameof(SavedSuccessMessage));
                    break;
                case nameof(Settings.FullscreenEditing):
                    RaisePropertyChanged(nameof(CanvasRectangle));
                    RaisePropertyChanged(nameof(UseFullScreenWidth));
                    RaisePropertyChanged(nameof(IsFullscreenToggle));
                    break;
                case nameof(Settings.FullscreenCapturing):
                    RaisePropertyChanged(nameof(CanvasRectangle));
                    RaisePropertyChanged(nameof(UseFullScreenWidth));
                    RaisePropertyChanged(nameof(IsFullscreenToggle));
                    break;
                case nameof(Settings.MaximumParallelWidth):
                    RaisePropertyChanged(nameof(CanvasRectangle));
                    break;
                case nameof(Settings.ChosenCamera):
                    PersistentStorage.Save(PersistentStorage.SETTINGS_KEY,Settings);
                    break;
            }

            if (e.PropertyName == nameof(Settings.Mode))
            {
                RaisePropertyChanged(nameof(IsFullscreenToggleVisible));
                RaisePropertyChanged(nameof(CaptureButtonPosition));
                RaisePropertyChanged(nameof(UseFullScreenWidth));
                RaisePropertyChanged(nameof(IsFullscreenToggle));
                RaisePropertyChanged(nameof(IsParallelTypeMode));
                RaisePropertyChanged(nameof(ShouldDonutGuideBeVisible));
                RaisePropertyChanged(nameof(ShouldPortraitViewModeWarningBeVisible));
                RaisePropertyChanged(nameof(IsPictureWiderThanTall));
                RaisePropertyChanged(nameof(MovementHintText));
            }
        }

        private void PairOperatorOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(PairOperator.PairStatus):
                    RaisePropertyChanged(nameof(ShouldCaptureButtonBeVisible));
                    RaisePropertyChanged(nameof(ShouldLineGuidesBeVisible));
                    RaisePropertyChanged(nameof(ShouldDonutGuideBeVisible));
                    RaisePropertyChanged(nameof(ShouldLeftLoadBeVisible));
                    RaisePropertyChanged(nameof(ShouldCenterLoadBeVisible));
                    RaisePropertyChanged(nameof(ShouldRightLoadBeVisible));
                    RaisePropertyChanged(nameof(IsFullscreenToggleVisible));
                    RaisePropertyChanged(nameof(ShouldPairButtonBeVisible));
                    RaisePropertyChanged(nameof(ShouldSwapSidesBeVisible));
                    RaisePropertyChanged(nameof(MovementHintText));
                    break;
            }
        }

        private void PairSettingsOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(PairSettings.IsPairedPrimary.HasValue):
                case nameof(PairSettings.IsPairedPrimary.Value):
                    RaisePropertyChanged(nameof(ShouldCaptureButtonBeVisible));
                    RaisePropertyChanged(nameof(IsFullscreenToggleVisible));
                    RaisePropertyChanged(nameof(ShouldDonutGuideBeVisible));
                    RaisePropertyChanged(nameof(ShouldLineGuidesBeVisible));
                    break;
            }
        }

        private void EditsSettingsOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(EditsSettings.ZoomMax):
                    RaisePropertyChanged(nameof(ZoomMax));
                    break;
                case nameof(EditsSettings.SideCropMax):
                    RaisePropertyChanged(nameof(SideCropMax));
                    break;
                case nameof(EditsSettings.TopOrBottomCropMax):
                    RaisePropertyChanged(nameof(TopOrBottomCropMax));
                    break;
                case nameof(EditsSettings.KeystoneMax):
                    RaisePropertyChanged(nameof(MaxKeystone));
                    RaisePropertyChanged(nameof(MinKeystone));
                    break;
                case nameof(EditsSettings.RotationMax):
                    RaisePropertyChanged(nameof(RotationMax));
                    RaisePropertyChanged(nameof(RotationMin));
                    break;
                case nameof(EditsSettings.VerticalAlignmentMax):
                    RaisePropertyChanged(nameof(VerticalAlignmentMax));
                    RaisePropertyChanged(nameof(VerticalAlignmentMin));
                    break;
            }
        }

        private void AlignmentSettingsOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(AlignmentSettings.IsAutomaticAlignmentOn) &&
                e.PropertyName != nameof(AlignmentSettings.ShowAdvancedAlignmentSettings1))
            {
                _isAlignmentInvalid = true;
            }
        }

        private void PairOperatorCountdownDisplayTimerCompleteSecondary(object sender, EventArgs e)
        {
            if (!PairOperator.IsPrimary)
            {
                IsHoldSteadySecondary = true;
            }
        }

        private void PairOperatorTransmissionComplete(object sender, EventArgs e)
        {
            IsHoldSteadySecondary = false;
            WorkflowStage = WorkflowStage.Capture;
        }

        private void PairOperatorTransmissionStarted(object sender, EventArgs e)
        {
            IsHoldSteadySecondary = false;
            WorkflowStage = WorkflowStage.Transmitting;
        }

        private void PairOperatorInitialSyncStarted(object sender, EventArgs e)
        {
            WorkflowStage = WorkflowStage.Syncing;
        }

        private void PairOperatorInitialSyncCompleted(object sender, EventArgs e)
        {
            //Debug.WriteLine("### PAIR OPERATOR INITIAL SYNC COMPLETED!!!!!");
            RestartPreviewTrigger = !RestartPreviewTrigger;
            TryTriggerMovementHint();
            WorkflowStage = WorkflowStage.Capture;
        }

        private void PairOperatorOnDisconnected(object sender, EventArgs e)
        {
            RaisePropertyChanged(nameof(ShouldLeftLeftRetakeBeVisible));
            RaisePropertyChanged(nameof(ShouldLeftRightRetakeBeVisible));
            RaisePropertyChanged(nameof(ShouldRightLeftRetakeBeVisible));
            RaisePropertyChanged(nameof(ShouldRightRightRetakeBeVisible));
            IsHoldSteadySecondary = false;
            if (WorkflowStage == WorkflowStage.Syncing || 
                WorkflowStage == WorkflowStage.Transmitting ||
                WorkflowStage == WorkflowStage.Loading)
            {
                WorkflowStage = WorkflowStage.Capture;
            }
            RestartPreviewTrigger = !RestartPreviewTrigger;
            RaisePropertyChanged(nameof(MovementHintText));
        }

        private void PairOperatorOnConnected(object sender, EventArgs e)
        {
            RemotePreviewFrame = null;
            ShowFovPreparationPopup();
            StopPreviewTrigger = !StopPreviewTrigger;
            RaisePropertyChanged(nameof(MovementHintText));
        }

        private void PairOperatorOnSyncRequested(object sender, EventArgs e)
        {
            WorkflowStage = WorkflowStage.Syncing;
        }

        private void PairOperatorOnPreviewFrameRequestReceived(object sender, EventArgs e)
        {
            if (WorkflowStage == WorkflowStage.Syncing)
            {
                WorkflowStage = WorkflowStage.Capture;
            }
        }

        private void PairOperatorTimeoutOccurred(object sender, ElapsedEventArgs e)
        {
            IsHoldSteadySecondary = false;
            FullWipe();
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
                    string loadType;
                    if (RightCapture == null ^ LeftCapture == null)
                    {
                        loadType = _singleSide;
                    }
                    else
                    {
                        loadType = await OpenLoadingPopup();
                    }

                    if (loadType == _cancel ||
                        loadType == null)
                    {
                        WorkflowStage = WorkflowStage.Capture;
                        return;
                    }

                    if (loadType == _singleSide)
                    {
                        using var stream = new SKMemoryStream(image1);
                        using var codec = SKCodec.Create(stream);
                        CheckCodecValidityAndFormat(codec);

                        LocalCapturedFrame = new IncomingFrame
                        {
                            Frame = SKBitmap.Decode(codec),
                            Orientation = codec.EncodedOrigin
                        };
                    }
                    else
                    {
                        await LoadFullStereoImage(image1);
                    }
                }
                else
                {
                    // i save left first, so i load left first
                    using var leftStream = new SKMemoryStream(image1);
                    using var leftCodec = SKCodec.Create(leftStream);
                    CheckCodecValidityAndFormat(leftCodec);

                    SetLeftBitmap(AutoOrient(SKBitmap.Decode(leftCodec), leftCodec.EncodedOrigin, false), true, true);

                    using var rightStream = new SKMemoryStream(image2);
                    using var rightCodec = SKCodec.Create(rightStream);
                    CheckCodecValidityAndFormat(rightCodec);

                    SetRightBitmap(AutoOrient(SKBitmap.Decode(rightCodec), rightCodec.EncodedOrigin, false), true, true);
                }
            }
            catch (InvalidImageException ex)
            {
                await CoreMethods.DisplayAlert(AppResources.Page_Camera_InvalidImage,
                    ex.Message, AppResources.Button_OK);
                WorkflowStage = WorkflowStage.Capture;
            }
            catch (Exception e)
            {
                Error = e;
            }
        }

        private static void CheckCodecValidityAndFormat(SKCodec codec)
        {
            if (codec == null) throw new InvalidImageException(AppResources.Page_Camera_SelectedInvalid);
            if (codec.EncodedFormat != SKEncodedImageFormat.Jpeg &&
                codec.EncodedFormat != SKEncodedImageFormat.Png &&
                codec.EncodedFormat != SKEncodedImageFormat.Webp)
            {
                throw new InvalidImageException(
                    AppResources.Page_Camera_PleaseSelectValid + codec.EncodedFormat + ".");
            }
        }

        protected override async void ViewIsAppearing(object sender, EventArgs e)
        {
            try
            {
                base.ViewIsAppearing(sender, e);
                DependencyService.Get<IScreenKeepAwaker>()?.KeepScreenAwake();
                _deviceDisplayWrapper.HoldScreenOn();
                TryTriggerMovementHint();

                if (WorkflowStage == WorkflowStage.Final)
                {
                    AutoAlign();
                }

                if (((Settings.Mode == DrawMode.Cross ||
                      Settings.Mode == DrawMode.RedCyanAnaglyph ||
                      Settings.Mode == DrawMode.GrayscaleRedCyanAnaglyph) &&
                     !WasCaptureCross ||
                     (Settings.Mode == DrawMode.Parallel ||
                      Settings.Mode == DrawMode.Cardboard) &&
                     WasCaptureCross) &&
                    LeftCapture != null &&
                    RightCapture != null)
                {
                    SwapSidesCommand.Execute(true);
                    WasCaptureCross = !WasCaptureCross;
                }

                PairOperator.CurrentCoreMethods = CoreMethods;

                await Task.Delay(100);
                await EvaluateAndShowWelcomePopup();

                AutoconnectIfOn();
            }
            catch (Exception ex)
            {
                Error = ex;
            }
        }

        private void AutoconnectIfOn()
        {
            if (Settings.PairSettings.Autoconnect &&
                PairOperator.PairStatus == PairStatus.Disconnected &&
                Settings.PairSettings.IsPairedPrimary.HasValue)
            {
                PairCommand.Execute(null);
            }
        }

        private async void ShowFovPreparationPopup()
        {
            try
            {
                if (!Settings.PairSettings.IsFovCorrectionSet &&
                    Settings.PairSettings.IsPairedPrimary.HasValue &&
                    Settings.PairSettings.IsPairedPrimary.Value &&
                    PairOperator.PairStatus == PairStatus.Connected)
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await CoreMethods.DisplayAlert(AppResources.Page_FovCorrection,
                            AppResources.Page_FovCorrectionExplain,
                            AppResources.Button_OK);
                    });
                }
            }
            catch (Exception e)
            {
                Error = e;
            }
        }

        private void PairOperatorOnErrorOccurred(object sender, ErrorEventArgs e)
        {
            Error = new Exception(e.Step, e.Exception);
            _secondaryErrorOccurred = true;
        }

        private void PairOperatorOnCapturedImageReceived(object sender, byte[] bytes)
        {
            //RemotePreviewFrame = null;
            var wasOtherSideFrontFacing = 
                RemotePreviewFrame?.IsFrontFacing ?? 
                LocalCapturedFrame?.IsFrontFacing ?? 
                LocalPreviewFrame?.IsFrontFacing == true;
            //LocalCapturedFrame = null;
            using var stream = new SKMemoryStream(bytes);
            using var codec = SKCodec.Create(stream);
            var bitmap = AutoOrient(
                SKBitmap.Decode(codec), codec.EncodedOrigin, wasOtherSideFrontFacing);

            var remoteCapturedFrame = new IncomingFrame
            {
                Frame = bitmap,
                IsFrontFacing = wasOtherSideFrontFacing,
                Orientation = codec.EncodedOrigin
            };

            Debug.WriteLine("### we have a remote captured frame");
            if (Settings.IsCaptureLeftFirst)
            {
                SetRightBitmap(remoteCapturedFrame.Frame, false, true);
            }
            else
            {
                SetLeftBitmap(remoteCapturedFrame.Frame, false, true);
            }
        }

        private void TryTriggerMovementHint(bool suppressWhenPaired = false)
        {
            if (LeftCapture == null ^ RightCapture == null && 
                !Settings.IsCaptureInMirrorMode ||
                PairOperator.PairStatus == PairStatus.Connected &&
                Settings.PairSettings.IsPairedPrimary.HasValue &&
                Settings.PairSettings.IsPairedPrimary.Value &&
                RightCapture == null &&
                LeftCapture == null &&
                !suppressWhenPaired || 
                Settings.IsCaptureInMirrorMode &&
                RightCapture == null &&
                LeftCapture == null)
            {
                if (Settings.Mode == DrawMode.Cardboard)
                {
                    MoveHintTriggerSide = !MoveHintTriggerSide;
                }
                else
                {
                    MoveHintTriggerCenter = !MoveHintTriggerCenter;
                }
            }
        }

        private async Task DrawAnaglyph(bool grayscale)
        {
            var overlayedSize = DrawTool.CalculateOverlayedImageSizeOrientedWithEditsNoBorder(Edits, Settings, LeftCapture,
                LeftAlignmentTransform, RightCapture, RightAlignmentTransform);
            using var tempSurface =
                SKSurface.Create(new SKImageInfo((int)overlayedSize.Width, (int)overlayedSize.Height));
            var canvas = tempSurface.Canvas;
            canvas.Clear(SKColor.Empty);

            DrawTool.DrawImagesOnCanvas(
                tempSurface, 
                LeftCapture, LeftAlignmentTransform,
                RightCapture, RightAlignmentTransform,
                Settings, Edits, grayscale ? DrawMode.GrayscaleRedCyanAnaglyph : DrawMode.RedCyanAnaglyph, WasCapturePaired);

            await SaveSurfaceSnapshot(tempSurface, grayscale ? AppResources.SaveModes_GrayscaleAnaglyph : AppResources.SaveModes_Anaglyph);
        }

        protected override void ViewIsDisappearing(object sender, EventArgs e)
        {
            PairOperator.ErrorOccurred -= PairOperatorOnErrorOccurred;
            DependencyService.Get<IScreenKeepAwaker>()?.LetScreenSleep();
            _deviceDisplayWrapper.DoNotHoldScreenOn();
            base.ViewIsDisappearing(sender, e);
        }

        private void PairOperatorOnPreviewFrameReceived(object sender, byte[] bytes)
        {
            RemotePreviewFrame = OrientAndDecodeIncomingFrame(bytes);
        }

        private static IncomingFrame OrientAndDecodeIncomingFrame(byte[] bytes)
        {
            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                using var stream = new MemoryStream(bytes, 0, bytes.Length - 1);
                var orientationByte = bytes.Last();
                return new IncomingFrame
                {
                    Orientation = (SKEncodedOrigin)orientationByte,
                    Frame = SKBitmap.Decode(stream)
                };
            }
            else
            {
                using var stream = new SKMemoryStream(bytes);
                using var codec = SKCodec.Create(stream);
                return new IncomingFrame
                {
                    Orientation = codec.EncodedOrigin,
                    Frame = SKBitmap.Decode(codec)
                };
            }
        }

        private async Task SaveSurfaceSnapshot(SKSurface surface, string methodModifier)
        {
            using var skImage = surface.Snapshot();
            using var encoded = skImage.Encode(SKEncodedImageFormat.Jpeg, 100);
            await _photoSaver.SavePhoto(
                encoded.ToArray(),
                Settings.SavingDirectory,
                Settings.SaveIntoDedicatedFolder2 ? "CrossCam" : Settings.SaveIntoSeparateFolders1 ? "CrossCam " + methodModifier : "",
                Settings.SaveToExternal);
        }

        private async Task<string> OpenLoadingPopup()
        {
            return await MainThread.InvokeOnMainThreadAsync(async () => await CoreMethods.DisplayActionSheet(AppResources.Page_Camera_ChooseAction, _cancel, null,
                _fullImage, _singleSide));
        }

        private async Task LoadFullStereoImage(byte[] image)
        {
            try
            {
                using var imageBitmap = SKBitmap.Decode(image);
                var removeBorder = false;
                if (Settings.PromptToClipOffDetectedBorder &&
                    DoesImageContainBorder(imageBitmap, 0, imageBitmap.Width - 1, 0, imageBitmap.Height - 1, out int whatever))
                {
                    removeBorder = await CoreMethods.DisplayAlert(AppResources.Page_Camera_BorderDetected, AppResources.Page_Camera_BorderAsk,
                        AppResources.Button_Camera_Remove, AppResources.Button_Camera_Keep);
                }
                var leftHalf = await Task.Run(() => GetHalfOfImage(image, true, removeBorder));
                SetLeftBitmap(leftHalf, false, true);
                var rightHalf = await Task.Run(() => GetHalfOfImage(image, false, removeBorder));
                SetRightBitmap(rightHalf, false, true);
            }
            catch (InvalidImageException)
            {
                throw;
            }
            catch (Exception e)
            {
                Error = e;
            }
        }

        private async void AutoAlign()
        {
            try
            {
                if (Settings.AlignmentSettings.IsAutomaticAlignmentOn &&
                    LeftCapture != null &&
                    RightCapture != null &&
                    _isAlignmentInvalid &&
                    0 == Interlocked.Exchange(ref _alignmentThreadLock, 1))
                {
                    _isAlignmentInvalid = false;
                    WorkflowStage = WorkflowStage.AutomaticAlign;

                    var openCv = DependencyService.Get<IOpenCv>();

                    AlignedResult alignedResult = null;
                    if (openCv?.IsOpenCvSupported() == true)
                    {
                        var firstImage = Settings.IsCaptureLeftFirst ? LeftCapture : RightCapture;
                        var secondImage = Settings.IsCaptureLeftFirst ? RightCapture : LeftCapture;
                        try
                        {
                            await Task.Run(() =>
                            {
                                if (Settings.AlignmentSettings.ForceEcc2)
                                {
                                    try
                                    {
                                        alignedResult = openCv.CreateAlignedSecondImageEcc(
                                            firstImage,
                                            secondImage,
                                            Settings.AlignmentSettings);
                                    }
                                    catch (Exception e)
                                    {
                                        Error = e;

                                        alignedResult = openCv.CreateAlignedSecondImageKeypoints(
                                            firstImage,
                                            secondImage,
                                            Settings.AlignmentSettings,
                                            Settings.IsCaptureLeftFirst &&
                                            Settings.Mode != DrawMode.Parallel ||
                                            !Settings.IsCaptureLeftFirst &&
                                            Settings.Mode == DrawMode.Parallel);
                                    }

                                    alignedResult ??= openCv.CreateAlignedSecondImageKeypoints(
                                        firstImage,
                                        secondImage,
                                        Settings.AlignmentSettings,
                                        Settings.IsCaptureLeftFirst &&
                                        Settings.Mode != DrawMode.Parallel ||
                                        !Settings.IsCaptureLeftFirst &&
                                        Settings.Mode == DrawMode.Parallel);
                                }
                                else
                                {
                                    try
                                    {
                                        alignedResult = openCv.CreateAlignedSecondImageKeypoints(
                                            firstImage,
                                            secondImage,
                                            Settings.AlignmentSettings,
                                            Settings.IsCaptureLeftFirst &&
                                            Settings.Mode != DrawMode.Parallel ||
                                            !Settings.IsCaptureLeftFirst &&
                                            Settings.Mode == DrawMode.Parallel);
                                    }
                                    catch (Exception e)
                                    {
                                        Error = e;

                                        alignedResult = openCv.CreateAlignedSecondImageEcc(
                                            firstImage,
                                            secondImage,
                                            Settings.AlignmentSettings);
                                    }

                                    alignedResult ??= openCv.CreateAlignedSecondImageEcc(
                                        firstImage,
                                        secondImage,
                                        Settings.AlignmentSettings);
                                }
                            });
                        }
                        catch (Exception e)
                        {
                            Error = e;
                        }

                        if (alignedResult != null)
                        {
                            ClearEdits();
                            if (alignedResult.Confidence > 0)
                            {
                                AlignmentConfidence = alignedResult.Confidence + "%";
                            }
                            else
                            {
                                AlignmentConfidence = "KP";
                            }

                            if (Settings.AlignmentSettings.DrawKeypointMatches &&
                                alignedResult.DrawnDirtyMatches != null)
                            {
                                var countPoint = new SKPoint(0, alignedResult.DrawnDirtyMatches.Height / 16f);
                                var countPaint = new SKPaint
                                {
                                    Color = SKColor.Parse("#00ff00"),
                                    TextSize = alignedResult.DrawnDirtyMatches.Height / 16f
                                };
                                using var dirtyMatchesSurface = SKSurface.Create(new SKImageInfo(
                                    alignedResult.DrawnDirtyMatches.Width, alignedResult.DrawnDirtyMatches.Height));

                                var dirtyMatchesCanvas = dirtyMatchesSurface.Canvas;
                                dirtyMatchesCanvas.Clear();

                                if (DeviceInfo.Platform == DevicePlatform.iOS && IsViewInverted)
                                {
                                    dirtyMatchesCanvas.RotateDegrees(180);
                                    dirtyMatchesCanvas.Translate(-1f * alignedResult.DrawnDirtyMatches.Width,
                                        -1f * alignedResult.DrawnDirtyMatches.Height);
                                }

                                dirtyMatchesCanvas.DrawBitmap(alignedResult.DrawnDirtyMatches, 0, 0);

                                dirtyMatchesCanvas.DrawText(
                                    alignedResult.DirtyMatchesCount.ToString(),
                                    countPoint,
                                    countPaint);

                                await SaveSurfaceSnapshot(dirtyMatchesSurface, AppResources.Page_Camera_KeyPoints);


                                if ((Settings.AlignmentSettings.DiscardOutliersBySlope2 ||
                                     Settings.AlignmentSettings.DiscardOutliersByDistance2) &&
                                    alignedResult.DrawnCleanMatches != null)
                                {
                                    using var cleanMatchesSurface =
                                        SKSurface.Create(new SKImageInfo(alignedResult.DrawnCleanMatches.Width,
                                            alignedResult.DrawnCleanMatches.Height));
                                    var cleanMatchesCanvas = cleanMatchesSurface.Canvas;
                                    cleanMatchesCanvas.Clear();

                                    if (DeviceInfo.Platform == DevicePlatform.iOS && IsViewInverted)
                                    {
                                        cleanMatchesCanvas.RotateDegrees(180);
                                        cleanMatchesCanvas.Translate(-1f * alignedResult.DrawnCleanMatches.Width,
                                            -1f * alignedResult.DrawnCleanMatches.Height);
                                    }

                                    cleanMatchesCanvas.DrawBitmap(alignedResult.DrawnCleanMatches, 0, 0);
                                    cleanMatchesCanvas.DrawText(
                                        alignedResult.CleanMatchesCount.ToString(),
                                        countPoint,
                                        countPaint);

                                    await SaveSurfaceSnapshot(cleanMatchesSurface, AppResources.Page_Camera_KeyPoints);
                                }
                            }

                            if (alignedResult.Warped1 != null &&
                                alignedResult.Warped2 != null &&
                                Settings.AlignmentSettings.DrawResultWarpedByOpenCv)
                            {
                                using var surface =
                                    SKSurface.Create(new SKImageInfo(alignedResult.Warped1.Width * 2,
                                        alignedResult.Warped1.Height));
                                surface.Canvas.DrawBitmap(alignedResult.Warped1, 0, 0);
                                surface.Canvas.DrawBitmap(alignedResult.Warped2, alignedResult.Warped1.Width, 0);
                                var textBlob = SKTextBlob.Create(
                                    Settings.AlignmentSettings.DownsizePercentage2 + " " + alignedResult.MethodName,
                                    new SKFont
                                    {
                                        Size = alignedResult.Warped1.Height / 5f
                                    });
                                surface.Canvas.DrawText(textBlob, alignedResult.Warped1.Height / 5f,
                                    alignedResult.Warped1.Height / 5f, new SKPaint
                                    {
                                        Color = SKColor.Parse("#00ff00"),
                                        TextSize = alignedResult.Warped1.Height / 5f,
                                        Style = SKPaintStyle.Fill
                                    });

                                await SaveSurfaceSnapshot(surface, AppResources.Page_Camera_Warped);
                            }

                            if (Settings.IsCaptureLeftFirst)
                            {
                                if (!alignedResult.TransformMatrix1.IsIdentity)
                                {
                                    LeftAlignmentTransform = alignedResult.TransformMatrix1;
                                }

                                RightAlignmentTransform = alignedResult.TransformMatrix2;
                            }
                            else
                            {
                                if (!alignedResult.TransformMatrix1.IsIdentity)
                                {
                                    RightAlignmentTransform = alignedResult.TransformMatrix1;
                                }

                                LeftAlignmentTransform = alignedResult.TransformMatrix2;
                            }

                            if (Settings.IsCaptureInMirrorMode)
                            {
                                MirrorModeAutoAlignWarningTrigger = !MirrorModeAutoAlignWarningTrigger;
                            }
                        }
                        else
                        {
                            AlignmentConfidence = "F";
                            ApplyFovCorrectionToZoom();
                            AlignmentFailFadeTrigger = !AlignmentFailFadeTrigger;
                        }
                    }
                    else
                    {
                        AlignmentConfidence = "n/a";
                        ApplyFovCorrectionToZoom();
                        AutomaticAlignmentNotSupportedTrigger = !AutomaticAlignmentNotSupportedTrigger;
                    }

                    WorkflowStage = WorkflowStage.Final;

                    _alignmentThreadLock = 0;
                }
            }
            catch (Exception e)
            {
                Error = e;
            }
        }

        private void ApplyFovCorrectionToZoom()
        {
            if (PairOperator.IsPrimary &&
                PairOperator.PairStatus == PairStatus.Connected)
            {
                if (Settings.IsCaptureLeftFirst)
                {
                    Edits.FovLeftCorrection = Settings.PairSettings.FovPrimaryCorrection;
                    Edits.FovRightCorrection = Settings.PairSettings.FovSecondaryCorrection;
                }
                else
                {
                    Edits.FovRightCorrection = Settings.PairSettings.FovPrimaryCorrection;
                    Edits.FovLeftCorrection = Settings.PairSettings.FovSecondaryCorrection;
                }
            }
        }

        private void SetLeftBitmap(SKBitmap bitmap, bool withMovementTrigger, bool stepForward)
        {
            if (bitmap == null) return;
            
            LeftCapture = bitmap;
            WasCapturePortrait = LeftCapture.Width < LeftCapture.Height;

            if (stepForward)
            {
                if (RightCapture == null)
                {
                    if (withMovementTrigger)
                    {
                        TryTriggerMovementHint();
                    }

                    if (PairOperator.PairStatus != PairStatus.Connected)
                    {
                        CameraColumn = 1;
                        WorkflowStage = WorkflowStage.Capture;
                    }
                }
                else
                {
                    if (WasCapturePaired &&
                        PairOperator.IsPrimary)
                    {
                        if (Settings.PairSettings.IsFovCorrectionSet)
                        {
                            CheckAndCorrectResolutionFovAndAspectDifferences();
                        }
                        else
                        {
                            WorkflowStage = WorkflowStage.FovCorrection;
                            ShowFovDialog();
                            return;
                        }
                    }
                    WasCaptureCross = Settings.Mode != DrawMode.Parallel;
                    CameraColumn = Settings.IsCaptureLeftFirst ? 0 : 1;
                    WorkflowStage = WorkflowStage.Final;
                    AutoAlign();
                    if (Settings.InstantSave)
                    {
                        SaveCapturesCommand.Execute(null);
                    }
                }
            }
        }

        private void SetRightBitmap(SKBitmap bitmap, bool withMovementTrigger, bool stepForward)
        {
            if (bitmap == null) return;

            RightCapture = bitmap;
            WasCapturePortrait = RightCapture.Width < RightCapture.Height;

            if (stepForward)
            {
                if (LeftCapture == null)
                {
                    if (withMovementTrigger)
                    {
                        TryTriggerMovementHint();
                    }

                    if (PairOperator.PairStatus != PairStatus.Connected)
                    {
                        CameraColumn = 0;
                        WorkflowStage = WorkflowStage.Capture;
                    }
                }
                else
                {
                    if (WasCapturePaired &&
                        PairOperator.IsPrimary)
                    {
                        if (Settings.PairSettings.IsFovCorrectionSet)
                        {
                            CheckAndCorrectResolutionFovAndAspectDifferences();
                        }
                        else
                        {
                            WorkflowStage = WorkflowStage.FovCorrection;
                            ShowFovDialog();
                            return;
                        }
                    }
                    WasCaptureCross = Settings.Mode != DrawMode.Parallel;
                    CameraColumn = Settings.IsCaptureLeftFirst ? 0 : 1;
                    WorkflowStage = WorkflowStage.Final;
                    AutoAlign();
                    if (Settings.InstantSave)
                    {
                        SaveCapturesCommand.Execute(null);
                    }
                }
            }
        }

        public void GenerateCardboardCacheIfNeeded()
        {
            if (RightCaptureCardboardCached == null ||
                LeftCaptureCardboardCached == null ||
                LeftAlignmentTransformCardboardCached == null ||
                RightAlignmentTransformCardboardCached == null)
            {
                RightCaptureCardboardCached = ShrinkBitmapToScreenSize(RightCapture);
                var rightProportion = RightCaptureCardboardCached.Width / (1f * RightCapture.Width);
#if __ANDROID__ || __IOS__ || __WINDOWS__
                RightAlignmentTransformCardboardCached = OpenCv.ScaleSkMatrix(RightAlignmentTransform, rightProportion);
#endif
                LeftCaptureCardboardCached = ShrinkBitmapToScreenSize(LeftCapture);
                var leftProportion = LeftCaptureCardboardCached.Width / (1f * LeftCapture.Width);
#if __ANDROID__ || __IOS__ || __WINDOWS__
                LeftAlignmentTransformCardboardCached = OpenCv.ScaleSkMatrix(LeftAlignmentTransform, leftProportion);
#endif
            }
        }

        private SKBitmap ShrinkBitmapToScreenSize(SKBitmap bitmap)
        {
            var smallerDimBitmap = Math.Min(bitmap.Width, bitmap.Height);
            var smallerDimScreen = Math.Min(DeviceDisplay.MainDisplayInfo.Width, DeviceDisplay.MainDisplayInfo.Height);
            var downsizeProportion = smallerDimBitmap / smallerDimScreen;
            var newWidth = bitmap.Width / downsizeProportion;
            var newHeight = bitmap.Height / downsizeProportion;

            var shrunk = new SKBitmap((int)newWidth,
                (int)newHeight);
            using var canvas = new SKCanvas(shrunk);
            canvas.DrawBitmap(
                bitmap, new SKRect(0, 0, shrunk.Width, shrunk.Height));
            return shrunk;
        }

        // TODO: remove this eventually, but right now it only happens once on final capture
        // TODO: and is necessary to use ECC alignment.

        // TODO: if keypoint alignment gets good it's going to need to NOT do zooming separately
        // TODO: in draw tool because the alignment matrix will handle it.
        private void CheckAndCorrectResolutionFovAndAspectDifferences()
        {
            if (!_isFovCorrected)
            {
                _isFovCorrected = true;
                float leftRatio;
                float rightRatio;
                if (LeftCapture.Width < LeftCapture.Height) //portrait
                {
                    leftRatio = LeftCapture.Height / (1f * LeftCapture.Width);
                    rightRatio = RightCapture.Height / (1f * RightCapture.Width);
                }
                else //landscape
                {
                    leftRatio = LeftCapture.Width / (1f * LeftCapture.Height);
                    rightRatio = RightCapture.Width / (1f * RightCapture.Height);
                }
                if (leftRatio != rightRatio)
                {
                    if (LeftCapture.Height > LeftCapture.Width) // portrait
                    {
                        if (leftRatio < rightRatio) // right is taller
                        {
                            var newWidth = (int)(LeftCapture.Height / rightRatio);
                            var corrected = new SKBitmap(newWidth, LeftCapture.Height);
                            using var canvas = new SKCanvas(corrected);
                            canvas.DrawBitmap(
                                LeftCapture,
                                new SKRect((LeftCapture.Width - newWidth) / 2f, 0, LeftCapture.Width - (LeftCapture.Width - newWidth) / 2f, LeftCapture.Height),
                                new SKRect(0, 0, newWidth, LeftCapture.Height));

                            LeftCapture = corrected;
                        }
                        else
                        {
                            var newWidth = (int)(RightCapture.Height / leftRatio);
                            var corrected = new SKBitmap(newWidth, RightCapture.Height);
                            using var canvas = new SKCanvas(corrected);
                            canvas.DrawBitmap(
                                RightCapture,
                                new SKRect((RightCapture.Width - newWidth) / 2f, 0, RightCapture.Width - (RightCapture.Width - newWidth) / 2f, RightCapture.Height),
                                new SKRect(0, 0, newWidth, RightCapture.Height));

                            RightCapture = corrected;
                        }
                    }
                    else //landscape
                    {
                        if (leftRatio > rightRatio) // left is wider
                        {
                            var newHeight = (int)(RightCapture.Width * leftRatio);
                            var corrected = new SKBitmap(RightCapture.Width, newHeight);
                            using var canvas = new SKCanvas(corrected);
                            canvas.DrawBitmap(
                                RightCapture,
                                new SKRect(0, (RightCapture.Height - newHeight) / 2f, RightCapture.Width, RightCapture.Height - (RightCapture.Height - newHeight) / 2f),
                                new SKRect(0, 0, RightCapture.Width, newHeight));

                            RightCapture = corrected;
                        }
                        else
                        {
                            var newHeight = (int)(LeftCapture.Width * rightRatio);
                            var corrected = new SKBitmap(LeftCapture.Width, newHeight);
                            using var canvas = new SKCanvas(corrected);
                            canvas.DrawBitmap(
                                LeftCapture,
                                new SKRect(0, (LeftCapture.Height - newHeight) / 2f, LeftCapture.Width, LeftCapture.Height - (LeftCapture.Height - newHeight) / 2f),
                                new SKRect(0, 0, LeftCapture.Width, newHeight));

                            LeftCapture = corrected;
                        }
                    }
                }

                if (Settings.PairSettings.IsFovCorrectionSet &&
                    (Settings.PairSettings.FovPrimaryCorrection != 0 ||
                     Settings.PairSettings.FovSecondaryCorrection != 0))
                {
                    double zoomAmount;
                    if (Settings.PairSettings.FovPrimaryCorrection > 0)
                    {
                        zoomAmount = Settings.PairSettings.FovPrimaryCorrection + 1;
                        if (Settings.IsCaptureLeftFirst)
                        {
                            LeftCapture = ZoomBitmap(zoomAmount, LeftCapture);
                        }
                        else
                        {
                            RightCapture = ZoomBitmap(zoomAmount, RightCapture);
                        }
                    }
                    else
                    {
                        zoomAmount = Settings.PairSettings.FovSecondaryCorrection + 1;
                        if (Settings.IsCaptureLeftFirst)
                        {
                            RightCapture = ZoomBitmap(zoomAmount, RightCapture);
                        }
                        else
                        {
                            LeftCapture = ZoomBitmap(zoomAmount, LeftCapture);
                        }
                    }
                }


                if (LeftCapture.Width < RightCapture.Width)
                {
                    var corrected = new SKBitmap(LeftCapture.Width, LeftCapture.Height);
                    using var canvas = new SKCanvas(corrected);
                    canvas.DrawBitmap(
                        RightCapture,
                        new SKRect(0, 0, LeftCapture.Width, LeftCapture.Height));

                    RightCapture = corrected;
                }
                else if (RightCapture.Width < LeftCapture.Width)
                {
                    var corrected = new SKBitmap(RightCapture.Width, RightCapture.Height);
                    using var surface = new SKCanvas(corrected);
                    surface.DrawBitmap(
                        LeftCapture,
                        new SKRect(0, 0, RightCapture.Width, RightCapture.Height));

                    LeftCapture = corrected;
                }
            }
        }

        private static SKBitmap ZoomBitmap(double zoom, SKBitmap originalBitmap)
        {
            var zoomedWidth = originalBitmap.Width * zoom;
            var zoomedHeight = originalBitmap.Height * zoom;

            var newX = (originalBitmap.Width - zoomedWidth) / 2;
            var newY = (originalBitmap.Height - zoomedHeight) / 2f;

            var corrected = new SKBitmap(originalBitmap.Width, originalBitmap.Height);
            using var surface = new SKCanvas(corrected);
            surface.DrawBitmap(originalBitmap,
                new SKRect(0, 0, originalBitmap.Width, originalBitmap.Height),
                new SKRect(
                    (float)newX,
                    (float)newY,
                    (float)(newX + zoomedWidth),
                    (float)(newY + zoomedHeight)));
            return corrected;
        }

        private async void ShowFovDialog()
        {
            try
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await CoreMethods.DisplayAlert(AppResources.Page_Camera_FovTitle,
                        AppResources.Page_Camera_FovExplain, AppResources.Button_OK);
                });
            }
            catch (Exception e)
            {
                Error = e;
            }
        }

        private static SKBitmap GetHalfOfImage(SKBitmap original, bool wantLeft, bool clipBorder, SKEncodedOrigin orientationToCorrect = SKEncodedOrigin.Default, bool withMirror = false, bool isFrontFacing = false)
        {
            if (original == null) return null;

            if (orientationToCorrect != SKEncodedOrigin.Default)
            {
                original = AutoOrient(original, orientationToCorrect, isFrontFacing);//TODO: it would be neat to use matrix stuff and shared code but i can't get it to work for all orientations - portrait is particularly weird
            }

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

            if (clipBorder &&
                DoesImageContainBorder(original, startX, endX, startY, endY, 
                    out var topLeftColor))
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
                    var color = original.GetPixel(startX + (endX - startX) / 4, ii); // 4 so as to not hit the guide dot if there is one
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

            var width = endX - startX - rightBorder - leftBorder;
            var height = endY - startY - topBorder - bottomBorder;

            var extracted = new SKBitmap(width, height);

            using var surface = new SKCanvas(extracted);

            var matrix = SKMatrix.CreateIdentity();
            if (withMirror)
            {
                var xFix = -original.Width / 4f;
                var yFix = -original.Height / 2f;
                matrix = matrix.PostConcat(SKMatrix.CreateTranslation(xFix, yFix));
                matrix = matrix.PostConcat(SKMatrix.CreateScale(-1, 1));
                matrix = matrix.PostConcat(SKMatrix.CreateTranslation(-xFix, -yFix));
            }

            surface.SetMatrix(matrix);
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

            return extracted;
        }

        private static bool DoesImageContainBorder(SKBitmap image, int startX, int endX, int startY, int endY, out int topLeftColor)
        {
            topLeftColor = GetTotalColor(image.GetPixel(startX, startY));
            var topRightColor = GetTotalColor(image.GetPixel(endX, startY));
            var bottomRightColor = GetTotalColor(image.GetPixel(endX, endY));
            var bottomLeftColor = GetTotalColor(image.GetPixel(startX, endY));

            return Math.Abs(topLeftColor - topRightColor) < BORDER_DIFF_THRESHOLD &&
                   Math.Abs(topRightColor - bottomRightColor) < BORDER_DIFF_THRESHOLD &&
                   Math.Abs(bottomRightColor - bottomLeftColor) < BORDER_DIFF_THRESHOLD &&
                   Math.Abs(bottomLeftColor - topLeftColor) < BORDER_DIFF_THRESHOLD &&
                   Math.Abs(topLeftColor - bottomRightColor) < BORDER_DIFF_THRESHOLD &&
                   Math.Abs(topRightColor - bottomLeftColor) < BORDER_DIFF_THRESHOLD;
        }

        private static SKBitmap AutoOrient(SKBitmap bitmap, SKEncodedOrigin origin, bool isFrontFacing)
        {
            SKBitmap rotated;
            switch (origin)
            {
                case SKEncodedOrigin.TopLeft: // also Default
                    rotated = new SKBitmap(bitmap.Width, bitmap.Height);
                    using (var surface = new SKCanvas(rotated))
                    {
                        if (isFrontFacing) surface.Scale(1, -1, bitmap.Width / 2f, bitmap.Height / 2f);
                        surface.DrawBitmap(bitmap, 0, 0);
                    }
                    return rotated;
                case SKEncodedOrigin.TopRight:
                    rotated = new SKBitmap(bitmap.Width, bitmap.Height);
                    using (var surface = new SKCanvas(rotated))
                    {
                        surface.Scale(-1, 1, bitmap.Width / 2f, bitmap.Height / 2f);
                        if (isFrontFacing) surface.Scale(1, -1, bitmap.Width / 2f, bitmap.Height / 2f); // theorized
                        surface.DrawBitmap(bitmap, 0, 0);
                    }
                    return rotated;
                case SKEncodedOrigin.BottomRight:
                    rotated = new SKBitmap(bitmap.Width, bitmap.Height);
                    using (var surface = new SKCanvas(rotated))
                    {
                        surface.RotateDegrees(180, bitmap.Width / 2f, bitmap.Height / 2f);
                        if (isFrontFacing) surface.Scale(1, -1, bitmap.Width / 2f, bitmap.Height / 2f);
                        surface.DrawBitmap(bitmap, 0, 0);
                    }
                    return rotated;
                case SKEncodedOrigin.BottomLeft:
                    rotated = new SKBitmap(bitmap.Width, bitmap.Height);
                    using (var surface = new SKCanvas(rotated))
                    {
                        surface.Scale(1, -1, bitmap.Width / 2f, bitmap.Height / 2f);
                        if (isFrontFacing) surface.Scale(1, -1, bitmap.Width / 2f, bitmap.Height / 2f); // theorized
                        surface.DrawBitmap(bitmap, 0, 0);
                    }
                    return rotated;
                case SKEncodedOrigin.LeftTop:
                    rotated = new SKBitmap(bitmap.Height, bitmap.Width);
                    using (var surface = new SKCanvas(rotated))
                    {
                        surface.Translate(0, rotated.Height);
                        surface.RotateDegrees(270);
                        surface.Scale(-1, 1, bitmap.Width / 2f, bitmap.Height / 2f);
                        if (isFrontFacing) surface.Scale(1, -1, bitmap.Width / 2f, bitmap.Height / 2f); // theorized
                        surface.DrawBitmap(bitmap, 0, 0);
                    }
                    return rotated;
                case SKEncodedOrigin.RightTop:
                    rotated = new SKBitmap(bitmap.Height, bitmap.Width);
                    using (var surface = new SKCanvas(rotated))
                    {
                        surface.Translate(rotated.Width, 0);
                        surface.RotateDegrees(90);
                        if (isFrontFacing) surface.Scale(1, -1, bitmap.Width / 2f, bitmap.Height / 2f);
                        surface.DrawBitmap(bitmap, 0, 0);
                    }
                    return rotated;
                case SKEncodedOrigin.RightBottom:
                    rotated = new SKBitmap(bitmap.Height, bitmap.Width);
                    using (var surface = new SKCanvas(rotated))
                    {
                        surface.Translate(rotated.Width, 0);
                        surface.RotateDegrees(90);
                        surface.Scale(-1, 1, bitmap.Width / 2f, bitmap.Height / 2f);
                        if (isFrontFacing) surface.Scale(1, -1, bitmap.Width / 2f, bitmap.Height / 2f); //theorized
                        surface.DrawBitmap(bitmap, 0, 0);
                    }
                    return rotated;
                case SKEncodedOrigin.LeftBottom:
                    rotated = new SKBitmap(bitmap.Height, bitmap.Width);
                    using (var surface = new SKCanvas(rotated))
                    {
                        surface.Translate(0, rotated.Height);
                        surface.RotateDegrees(270);
                        if(isFrontFacing) surface.Scale(1, -1, bitmap.Width / 2f, bitmap.Height / 2f);
                        surface.DrawBitmap(bitmap, 0, 0);
                    }
                    return rotated;
                default:
                    return bitmap;
            }
        }

        private static SKBitmap GetHalfOfImage(byte[] bytes, bool wantLeft, bool clipBorder)
        {
            using var stream = new SKMemoryStream(bytes);
            using var codec = SKCodec.Create(stream);
            CheckCodecValidityAndFormat(codec);

            var original = SKBitmap.Decode(codec);
            return GetHalfOfImage(original, wantLeft, clipBorder, codec.EncodedOrigin);
        }

        private static int GetTotalColor(SKColor color)
        {
            return color.Red + color.Green + color.Blue;
        }

        private async Task EvaluateAndShowWelcomePopup()
        {
#if DEBUG
            await Task.CompletedTask;
#else
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (!Settings.HasOfferedTechniqueHelpBefore2)
                {
                    var showTechniquePage = await CoreMethods.DisplayAlert(AppResources.Page_Camera_Welcome,
                        AppResources.Page_Camera_WelcomeExplain, 
                        AppResources.Page_Camera_Technique, AppResources.Page_Camera_Directions);
                    Settings.HasOfferedTechniqueHelpBefore2 = true;
                    PersistentStorage.Save(PersistentStorage.SETTINGS_KEY, Settings);
                    if (showTechniquePage)
                    {
                        await CoreMethods.PushPageModel<TechniqueHelpViewModel>(Settings);
                    }
                    else
                    {
                        await CoreMethods.PushPageModel<DirectionsViewModel>(Settings);
                        Settings.HasShownDirectionsBefore = true;
                        PersistentStorage.Save(PersistentStorage.SETTINGS_KEY, Settings);
                    }
                }
                else
                {
                    if (!Settings.HasShownDirectionsBefore)
                    {
                        await CoreMethods.PushPageModel<DirectionsViewModel>(Settings);
                        Settings.HasShownDirectionsBefore = true;
                        PersistentStorage.Save(PersistentStorage.SETTINGS_KEY, Settings);
                    }
                }
            });
#endif
        }

        private void ClearCrops()
        {
            Edits.OutsideCrop = 0;
            Edits.InsideCrop = 0;
            Edits.RightCrop = 0;
            Edits.LeftCrop = 0;
            Edits.TopCrop = 0;
            Edits.BottomCrop = 0;
            CropMode = CropMode.Inside;
        }

        private void ClearManualAlignments()
        {
            Edits.LeftRotation = 0;
            Edits.RightRotation = 0;
            Edits.LeftZoom = 0;
            Edits.RightZoom = 0;
            Edits.VerticalAlignment = 0;
            ManualAlignMode = ManualAlignMode.VerticalAlign;
        }

        private void ClearAutoAlignment()
        {
            LeftAlignmentTransform = SKMatrix.Identity;
            RightAlignmentTransform = SKMatrix.Identity;
            AlignmentConfidence = "";
            _isAlignmentInvalid = true;
        }

        private void ClearKeystone()
        {
            Edits.Keystone = 0;
        }

        private void ClearEdits()
        {
            ClearCrops();
            ClearManualAlignments();
            ClearKeystone();
        }

        private void ClearEverythingButCaptures()
        {
            ClearEdits();
            ClearAutoAlignment();
            Explore.Clear();
        }

        private void FullWipe()
        {
            CameraColumn = Settings.IsCaptureLeftFirst ? 0 : 1;

            LeftCapture = null;
            LeftAlignmentTransform = SKMatrix.Identity;

            RightCapture = null;
            RightAlignmentTransform = SKMatrix.Identity;
            
            LocalCapturedFrame = null;

            ClearEverythingButCaptures();
            _secondaryErrorOccurred = false;
            WorkflowStage = WorkflowStage.Capture;
            WasCapturePaired = false;
            _isFovCorrected = false;
            if (Settings.IsTapToFocusEnabled2)
            {
                SwitchToContinuousFocusTrigger = !SwitchToContinuousFocusTrigger;
            }
            TryTriggerMovementHint();
        }
    }
}