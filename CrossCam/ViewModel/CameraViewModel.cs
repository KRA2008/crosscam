using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using CrossCam.CustomElement;
using CrossCam.Model;
using CrossCam.Page;
using CrossCam.Wrappers;
using FreshMvvm;
using Newtonsoft.Json;
using Plugin.DeviceInfo;
using SkiaSharp;
using Xamarin.Essentials;
using Xamarin.Forms;
using ErrorEventArgs = CrossCam.CustomElement.ErrorEventArgs;
using Exception = System.Exception;

namespace CrossCam.ViewModel
{
    public sealed class CameraViewModel : FreshBasePageModel
    {
        private const string FULL_IMAGE = "Load full stereo image";
        private const string SINGLE_SIDE = "Load single side";
        private const string CANCEL = "Cancel";

        public static BluetoothOperator BluetoothOperator;
        public BluetoothOperator BluetoothOperatorBindable => BluetoothOperator;

        public WorkflowStage WorkflowStage { get; set; }
        public CropMode CropMode { get; set; }
        public ManualAlignMode ManualAlignMode { get; set; }
        public KeystoneMode KeystoneMode { get; set; }
        public CameraSettingMode CameraSettingMode { get; set; }
        public FovCorrectionMode FovCorrectionMode { get; set; }

        public SKBitmap LeftBitmap { get; set; }
        public Command RetakeLeftCommand { get; set; }
        public bool LeftCaptureSuccess { get; set; }
        
        public SKBitmap RightBitmap { get; set; }
        public Command RetakeRightCommand { get; set; }
        public bool RightCaptureSuccess { get; set; }

        private SKBitmap _originalUnalignedBitmap { get; set; }

        public byte[] CapturedImageBytes { get; set; }
        public bool CaptureSuccess { get; set; }
        public int CameraColumn { get; set; }

        public byte[] PreviewFrame { get; set; }

        public AbsoluteLayoutFlags CanvasRectangleFlags => Settings.Mode == DrawMode.Parallel
            ? AbsoluteLayoutFlags.YProportional | AbsoluteLayoutFlags.HeightProportional | AbsoluteLayoutFlags.XProportional : 
            AbsoluteLayoutFlags.All;
        public Rectangle CanvasRectangle 
        {
            get 
            {
                if(Settings.Mode != DrawMode.Parallel) return new Rectangle(0,0,1,1);
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
                return new Rectangle(0.5, 0, Math.Min(Settings.MaximumParallelWidth, windowWidth), 1);
            }
        }

        public double PreviewBottomY { get; set; }

        public Command CapturePictureCommand { get; set; }
        public bool CapturePictureTrigger { get; set; }

        public bool MoveHintTrigger { get; set; }
        public bool WasSwipedTrigger { get; set; }

        public Command SaveCapturesCommand { get; set; }

        public Command ToggleViewModeCommand { get; set; }

        public Command ClearCapturesCommand { get; set; }

        public Command OpenCameraSettingsCommand { get; set; }
        public Command SaveCameraSettingCommand { get; set; }
        public Command ResetCameraSettingCommand { get; set; }
        public Command<CameraSettingMode> SetCameraSettingModeCommand { get; set; }
        public bool CameraSettingsVisible { get; set; }
        public ObservableCollection<AvailableCamera> AvailableCameras { get; set; }
        public AvailableCamera ChosenCamera { get; set; }

        public Command NavigateToSettingsCommand { get; set; }
        public Command NavigateToHelpCommand { get; set; }

        public Command SwapSidesCommand { get; set; }

        public Command GoToModeCommand { get; set; }
        public Command SaveEditCommand { get; set; }

        public Command ClearEditCommand { get; set; }

        public Command PromptForPermissionAndSendErrorEmailCommand { get; set; }
        public string ErrorMessage { get; set; }

        public Settings Settings { get; set; }

        public Edits Edits { get; set; }

        public Command SetManualAlignMode { get; set; }

        public Command SetFovCorrectionMode { get; set; }

        public Command PairCommand { get; set; }

        public double ZoomMax => 1 / 4d;
        public double SideCropMax => 1 / 2d;
        public double TopOrBottomCropMax => 1 / 2d;

        public Command SetCropMode { get; set; }

        public double VerticalAlignmentMax => 1 / 8d;
        public double VerticalAlignmentMin => -VerticalAlignmentMax;

        public float RotationMax => 5;
        public float RotationMin => -RotationMax;

        public Command SetKeystoneMode { get; set; }

        public float MaxKeystone => 1 / 4f;

        public Command LoadPhotoCommand { get; set; }

        public bool IsViewPortrait { get; set; }
        public bool IsViewInverted { get; set; }
        public bool WasCapturePortrait { get; set; }
        public bool WasCaptureCross { get; set; }

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
        public bool ShouldLeftPairBeVisible => IsNothingCaptured && Settings.Handedness == Handedness.Right;
        public bool ShouldRightPairBeVisible => IsNothingCaptured && Settings.Handedness == Handedness.Left;
        public bool ShouldLeftLeftRetakeBeVisible => LeftBitmap != null && (WorkflowStage == WorkflowStage.Final && DoesCaptureOrientationMatchViewOrientation || WorkflowStage == WorkflowStage.Capture && (Settings.Handedness == Handedness.Right || Settings.Handedness == Handedness.Center));
        public bool ShouldLeftRightRetakeBeVisible => LeftBitmap != null && WorkflowStage == WorkflowStage.Capture && Settings.Handedness == Handedness.Left;
        public bool ShouldRightLeftRetakeBeVisible => RightBitmap != null && WorkflowStage == WorkflowStage.Capture && Settings.Handedness == Handedness.Right;
        public bool ShouldRightRightRetakeBeVisible => RightBitmap != null && (WorkflowStage == WorkflowStage.Final && DoesCaptureOrientationMatchViewOrientation || WorkflowStage == WorkflowStage.Capture && (Settings.Handedness == Handedness.Left || Settings.Handedness == Handedness.Center));
        
        public bool ShouldCenterLoadBeVisible => WorkflowStage == WorkflowStage.Capture && Settings.Handedness != Handedness.Center && BluetoothOperator.PairStatus != PairStatus.Connected;
        public bool ShouldLeftLoadBeVisible => CameraColumn == 0 && WorkflowStage == WorkflowStage.Capture && Settings.Handedness == Handedness.Center && BluetoothOperator.PairStatus != PairStatus.Connected;
        public bool ShouldRightLoadBeVisible => CameraColumn == 1 && WorkflowStage == WorkflowStage.Capture && Settings.Handedness == Handedness.Center && BluetoothOperator.PairStatus != PairStatus.Connected;
        
        public bool DoesCaptureOrientationMatchViewOrientation => WasCapturePortrait == IsViewPortrait;
        public bool ShouldSettingsAndHelpBeVisible => !IsBusy && 
                                                      WorkflowStage != WorkflowStage.View;
        public bool IsExactlyOnePictureTaken => LeftBitmap == null ^ RightBitmap == null;
        public bool ShouldRightCaptureBeVisible => WorkflowStage == WorkflowStage.Capture && Settings.Handedness == Handedness.Right && (BluetoothOperatorBindable.PairStatus == PairStatus.Disconnected || Settings.IsPairedPrimary.HasValue && Settings.IsPairedPrimary.Value);
        public bool ShouldCenterCaptureBeVisible => WorkflowStage == WorkflowStage.Capture && Settings.Handedness == Handedness.Center && (BluetoothOperatorBindable.PairStatus == PairStatus.Disconnected || Settings.IsPairedPrimary.HasValue && Settings.IsPairedPrimary.Value);
        public bool ShouldLeftCaptureBeVisible => WorkflowStage == WorkflowStage.Capture && Settings.Handedness == Handedness.Left && (BluetoothOperatorBindable.PairStatus == PairStatus.Disconnected || Settings.IsPairedPrimary.HasValue && Settings.IsPairedPrimary.Value);

        public bool ShouldLineGuidesBeVisible => ((IsNothingCaptured && Settings.ShowGuideLinesWithFirstCapture)
                                                  || (IsExactlyOnePictureTaken && WorkflowStage != WorkflowStage.Loading)
                                                  || (BluetoothOperator.IsPrimary && BluetoothOperator.PairStatus == PairStatus.Connected && WorkflowStage == WorkflowStage.Capture))
                                                  && Settings.AreGuideLinesVisible
                                                  || WorkflowStage == WorkflowStage.Keystone
                                                  || WorkflowStage == WorkflowStage.ManualAlign
                                                  || WorkflowStage == WorkflowStage.FovCorrection;
        public bool ShouldDonutGuideBeVisible => ((IsNothingCaptured && Settings.ShowGuideDonutWithFirstCapture)
                                                 || (IsExactlyOnePictureTaken && WorkflowStage != WorkflowStage.Loading)
                                                 || (BluetoothOperator.IsPrimary && BluetoothOperator.PairStatus == PairStatus.Connected && WorkflowStage == WorkflowStage.Capture))
                                                 && Settings.IsGuideDonutVisible;

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
                                                          Settings.SaveForRedCyanAnaglyph ||
                                                          Settings.SaveForGrayscaleAnaglyph);

        public bool ShouldPairPreviewBeVisible => BluetoothOperator.PairStatus == PairStatus.Connected &&
                                                  BluetoothOperator.IsPrimary &&
                                                  WorkflowStage == WorkflowStage.Capture;

        public int IconColumn => CameraColumn == 0 ? 1 : 0;

        public bool ShouldPortraitViewModeWarningBeVisible => IsViewPortrait && 
                                                              IsPictureWiderThanTall &&
                                                              WorkflowStage != WorkflowStage.Saving &&
                                                              (WorkflowStage == WorkflowStage.Final ||
                                                                WorkflowStage == WorkflowStage.Edits) && 
                                                               Settings.Mode != DrawMode.GrayscaleRedCyanAnaglyph &&
                                                               Settings.Mode != DrawMode.RedCyanAnaglyph;
        private bool IsPictureWiderThanTall => LeftBitmap != null &&
                                               RightBitmap != null &&
                                               Settings.Mode != DrawMode.Parallel && 
                                               DrawTool.CalculateJoinedCanvasWidthLessBorder(LeftBitmap, RightBitmap, Edits) >
                                               DrawTool.CalculateCanvasHeightLessBorder(LeftBitmap, RightBitmap, Edits);

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
        private bool _secondaryErrorOccurred;

        public CameraViewModel()
        {
            _photoSaver = DependencyService.Get<IPhotoSaver>();

            Settings = PersistentStorage.LoadOrDefault(PersistentStorage.SETTINGS_KEY, new Settings());
            Edits = new Edits(Settings);
            BluetoothOperator = new BluetoothOperator(Settings);
            BluetoothOperator.Connected += BluetoothOperatorOnConnected;
            BluetoothOperator.Disconnected += BluetoothOperatorOnDisconnected;
            BluetoothOperator.PreviewFrameReceived += BluetoothOperatorOnPreviewFrameReceived;
            BluetoothOperator.CapturedImageReceived += BluetoothOperatorOnCapturedImageReceived;
            BluetoothOperator.InitialSyncStarted += BluetoothOperatorInitialSyncStarted;
            BluetoothOperator.InitialSyncCompleted += BluetoothOperatorInitialSyncCompleted;
            BluetoothOperator.TransmissionStarted += BluetoothOperatorTransmissionStarted;
            BluetoothOperator.TransmissionComplete += BluetoothOperatorTransmissionComplete;

            CameraColumn = Settings.IsCaptureLeftFirst ? 0 : 1;
            AvailableCameras = new ObservableCollection<AvailableCamera>();

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
                    if (_secondaryErrorOccurred)
                    {
                        ClearCaptures();
                    }
                    else
                    {
                        if (CameraColumn == 0)
                        {
                            LeftBytesCaptured(CapturedImageBytes, BluetoothOperator.PairStatus == PairStatus.Disconnected); // not awaiting. ok.
                        }
                        else
                        {
                            RightBytesCaptured(CapturedImageBytes, BluetoothOperator.PairStatus == PairStatus.Disconnected); // not awaiting. ok.
                        }

                        if (BluetoothOperator.IsPrimary &&
                            BluetoothOperator.PairStatus == PairStatus.Connected)
                        {
                            WorkflowStage = WorkflowStage.Loading;
                            RaisePropertyChanged(nameof(ShouldLineGuidesBeVisible));
                            RaisePropertyChanged(nameof(ShouldDonutGuideBeVisible));
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
                else if (args.PropertyName == nameof(Settings))
                {
                    var alignmentWasRunButIsOffNow = !Settings.IsAutomaticAlignmentOn &&
                                                      (_wasAlignmentWithoutHorizontalRun ||
                                                       _wasAlignmentWithHorizontalRun);
                    var otherAlignmentModeWasRun = Settings.IsAutomaticAlignmentOn &&
                                                   (_wasAlignmentWithHorizontalRun && !Settings.SaveForRedCyanAnaglyph && !Settings.AlignHorizontallySideBySide ||
                                                    _wasAlignmentWithoutHorizontalRun && (Settings.SaveForRedCyanAnaglyph || Settings.AlignHorizontallySideBySide));
                    if (alignmentWasRunButIsOffNow ||
                        otherAlignmentModeWasRun)
                    {
                        ClearCrops(true);
                        if (Settings.IsCaptureLeftFirst)
                        {
                            SetRightBitmap(_originalUnalignedBitmap, true, true); //calls autoalign internally
                        }
                        else
                        {
                            SetLeftBitmap(_originalUnalignedBitmap, true, true); //calls autoalign internally
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
                try
                {
                    if (BluetoothOperator.PairStatus == PairStatus.Connected)
                    {
                        ClearCaptures();
                    }
                    else
                    {
                        ClearEdits();
                        CameraColumn = 0;
                        LeftBitmap?.Dispose();
                        LeftBitmap = null;
                        TriggerMovementHint();
                        WorkflowStage = WorkflowStage.Capture;
                    }
                }
                catch (Exception e)
                {
                    ErrorMessage = e.ToString();
                }
            });

            RetakeRightCommand = new Command(() =>
            {
                try
                {
                    if (BluetoothOperator.PairStatus == PairStatus.Connected)
                    {
                        ClearCaptures();
                    }
                    else
                    {
                        ClearEdits();
                        CameraColumn = 1;
                        RightBitmap?.Dispose();
                        RightBitmap = null;
                        TriggerMovementHint();
                        WorkflowStage = WorkflowStage.Capture;
                    }
                }
                catch (Exception e)
                {
                    ErrorMessage = e.ToString();
                }
            });

            GoToModeCommand = new Command<WorkflowStage>(arg =>
            {
                WorkflowStage = arg;
            });

            SaveEditCommand = new Command(() =>
            {
                switch (WorkflowStage)
                {
                    case WorkflowStage.Edits:
                        WorkflowStage = WorkflowStage.Final;
                        break;
                    case WorkflowStage.FovCorrection:
                        Settings.IsFovCorrectionSet = true;
                        PersistentStorage.Save(PersistentStorage.SETTINGS_KEY, Settings);
                        WorkflowStage = WorkflowStage.Final;
                        Edits.VerticalAlignment = 0;
                        AutoAlign();
                        break;
                    default:
                        WorkflowStage = WorkflowStage.Edits;
                        break;
                }
            });

            ClearEditCommand = new Command(() =>
            {
                try
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
                }
                catch (Exception e)
                {
                    ErrorMessage = e.ToString();
                }
            });

            ClearCapturesCommand = new Command(async() =>
            {
                try
                {
                    var confirmClear = await CoreMethods.DisplayAlert("Really clear?",
                        "Are you sure you want to clear your pictures and start over?", "Yes, clear", "No");
                    if (confirmClear)
                    {
                        ClearCaptures();
                    }
                }
                catch (Exception e)
                {
                    ErrorMessage = e.ToString();
                }
            });

            CapturePictureCommand = new Command(() =>
            {
                if (BluetoothOperator.IsPrimary &&
                    BluetoothOperator.PairStatus == PairStatus.Connected)
                {
                    BluetoothOperator.BeginSyncedCapture();
                }
                else
                {
                    CapturePictureTrigger = !CapturePictureTrigger;
                }
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
                await CoreMethods.PushPageModel<HelpViewModel>(Settings);
            });

            OpenCameraSettingsCommand = new Command(() =>
            {
                CameraSettingsVisible = !CameraSettingsVisible;
                if (!CameraSettingsVisible)
                {
                    CameraSettingMode = CameraSettingMode.Menu;
                }
            });

            SetCameraSettingModeCommand = new Command<CameraSettingMode>(mode =>
            {
                CameraSettingMode = mode;
            });

            SaveCameraSettingCommand = new Command(() =>
            {
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
                var forced = obj as bool?;
                if (WorkflowStage == WorkflowStage.Capture ||
                    WorkflowStage == WorkflowStage.Final ||
                    WorkflowStage == WorkflowStage.Edits ||
                    forced.HasValue && forced.Value)
                {
                    var tempArray = LeftBitmap;
                    LeftBitmap = RightBitmap;
                    RightBitmap = tempArray;

                    if (WorkflowStage == WorkflowStage.Capture)
                    {
                        CameraColumn = CameraColumn == 0 ? 1 : 0;
                    }

                    var tempCrop = Edits.InsideCrop;
                    Edits.InsideCrop = Edits.OutsideCrop;
                    Edits.OutsideCrop = tempCrop;

                    var tempRotate = Edits.LeftRotation;
                    Edits.LeftRotation = Edits.RightRotation;
                    Edits.RightRotation = tempRotate;

                    var tempKeystone = Edits.LeftKeystone;
                    Edits.LeftKeystone = Edits.RightKeystone;
                    Edits.RightKeystone = tempKeystone;

                    var tempZoom = Edits.LeftZoom;
                    Edits.LeftZoom = Edits.RightZoom;
                    Edits.RightZoom = tempZoom;

                    Edits.VerticalAlignment = -Edits.VerticalAlignment;

                    Settings.IsCaptureLeftFirst = !Settings.IsCaptureLeftFirst;
                    Settings.RaisePropertyChanged(nameof(Settings.IsCaptureLeftFirst));
                    PersistentStorage.Save(PersistentStorage.SETTINGS_KEY, Settings);

                    TriggerMovementHint();
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

            SetFovCorrectionMode = new Command(mode =>
            {
                FovCorrectionMode = (FovCorrectionMode) mode;
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
                                if (Settings.IsCaptureLeftFirst)
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

                        var finalImageWidth = DrawTool.CalculateJoinedCanvasWidthLessBorder(LeftBitmap, RightBitmap, Edits);
                        var borderThickness = Settings.AddBorder
                            ? (int) (DrawTool.BORDER_CONVERSION_FACTOR * Settings.BorderWidthProportion *
                                     finalImageWidth)
                            : 0;
                        finalImageWidth += 3 * borderThickness;
                        var finalImageHeight = DrawTool.CalculateCanvasHeightLessBorder(LeftBitmap, RightBitmap, Edits) +
                                               2 * borderThickness;

                        finalImageWidth = (int) (finalImageWidth * (Settings.ResolutionProportion / 100d));
                        finalImageHeight = (int) (finalImageHeight * (Settings.ResolutionProportion / 100d));

                        if (Settings.SaveForCrossView &&
                            Settings.Mode == DrawMode.Cross ||
                            Settings.SaveForParallel &&
                            Settings.Mode == DrawMode.Parallel ||
                            Settings.SaveForCrossView && 
                            Settings.Mode == DrawMode.RedCyanAnaglyph ||
                            Settings.SaveForCrossView &&
                            Settings.Mode == DrawMode.GrayscaleRedCyanAnaglyph)
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

                                DrawTool.DrawImagesOnCanvas(
                                    canvas, LeftBitmap, RightBitmap, 
                                    Settings,
                                    Edits, 
                                    DrawMode.Cross);

                                await SaveSurfaceSnapshot(tempSurface);
                            }
                        }

                        if (Settings.SaveForParallel &&
                            Settings.Mode == DrawMode.Cross ||
                            Settings.SaveForCrossView &&
                            Settings.Mode == DrawMode.Parallel)
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

                                DrawTool.DrawImagesOnCanvas(
                                    canvas, LeftBitmap, RightBitmap, 
                                    Settings, 
                                    Edits, 
                                    DrawMode.Parallel);

                                await SaveSurfaceSnapshot(tempSurface);
                            }
                        }

                        if (Settings.SaveForRedCyanAnaglyph)
                        {
                            await DrawAnaglyph(false);
                        }

                        if (Settings.SaveForGrayscaleAnaglyph)
                        {
                            await DrawAnaglyph(true);
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

                                canvas.DrawBitmap(Settings.IsCaptureLeftFirst ? LeftBitmap : RightBitmap, 0, 0);

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
                await Device.InvokeOnMainThreadAsync(async () =>
                {
                    var sendReport = await CoreMethods.DisplayAlert("Oops",
                        "Sorry, CrossCam did an error. Please send me an error report so I can fix it!", "Send", "Don't Send");
                    if (sendReport)
                    {
                        var errorMessage = ErrorMessage + "\n" +
                                           "\n" +
                                           "Device Platform: " + DeviceInfo.Platform + "\n" +
                                           "Device Manufacturer: " + DeviceInfo.Manufacturer + "\n" +
                                           "Device Model: " + DeviceInfo.Model + "\n" +
                                           "Device Width: " + Application.Current.MainPage.Width + "\n" +
                                           "Device Height: " + Application.Current.MainPage.Height + "\n" +
                                           "OS Version Number: " + DeviceInfo.Version + "\n" +
                                           "OS Version String: " + DeviceInfo.VersionString + "\n" +
                                           "App Version: " + CrossDeviceInfo.Current.AppVersion + "\n" +
                                           "App Build: " + CrossDeviceInfo.Current.AppBuild + "\n" +
                                           "Idiom: " + CrossDeviceInfo.Current.Idiom + " \n" +
                                           "Settings: " + JsonConvert.SerializeObject(Settings);
                        Device.OpenUri(new Uri("mailto:me@kra2008.com?subject=CrossCam%20error%20report&body=" +
                                               HttpUtility.UrlEncode(errorMessage)));
                    }

                    ErrorMessage = null;
                });
            });

            PairCommand = new Command(async () =>
            {
                if (BluetoothOperator.PairStatus == PairStatus.Disconnected)
                {
                    if (!Settings.IsPairedPrimary.HasValue)
                    {
                        await CoreMethods.DisplayAlert("Pair Role Not Selected",
                            "Please go to Pairing page (via the Settings page) and choose a pairing role for this device before attempting to pair.",
                            "Ok");
                    }
                    else
                    {
                        if (Settings.IsPairedPrimary.Value)
                        {
                            await BluetoothOperator.SetUpPrimaryForPairing();
                        }
                        else
                        {
                            await BluetoothOperator.SetUpSecondaryForPairing();
                        }
                    }
                }
                else
                {
                    BluetoothOperator.Disconnect();
                }
            });
        }

        private void BluetoothOperatorTransmissionComplete(object sender, EventArgs e)
        {
            WorkflowStage = WorkflowStage.Capture;
        }

        private void BluetoothOperatorTransmissionStarted(object sender, EventArgs e)
        {
            WorkflowStage = WorkflowStage.Transmitting;
        }

        private void BluetoothOperatorInitialSyncStarted(object sender, EventArgs e)
        {
            WorkflowStage = WorkflowStage.Syncing;
        }

        private void BluetoothOperatorInitialSyncCompleted(object sender, EventArgs e)
        {
            TriggerMovementHint();
            WorkflowStage = WorkflowStage.Capture;
        }

        private void BluetoothOperatorOnDisconnected(object sender, EventArgs e)
        {
            if (WorkflowStage == WorkflowStage.Syncing)
            {
                WorkflowStage = WorkflowStage.Capture;
            }
            RaisePropertyChanged(nameof(ShouldLeftCaptureBeVisible));
            RaisePropertyChanged(nameof(ShouldCenterCaptureBeVisible));
            RaisePropertyChanged(nameof(ShouldRightCaptureBeVisible));
            RaisePropertyChanged(nameof(ShouldLineGuidesBeVisible));
            RaisePropertyChanged(nameof(ShouldPairPreviewBeVisible));
            RaisePropertyChanged(nameof(ShouldDonutGuideBeVisible));
            RaisePropertyChanged(nameof(ShouldLeftLoadBeVisible));
            RaisePropertyChanged(nameof(ShouldCenterLoadBeVisible));
            RaisePropertyChanged(nameof(ShouldRightLoadBeVisible));
        }

        private void BluetoothOperatorOnConnected(object sender, EventArgs e)
        {
            ShowFovPreparationPopup();
            RaisePropertyChanged(nameof(ShouldLeftCaptureBeVisible));
            RaisePropertyChanged(nameof(ShouldCenterCaptureBeVisible));
            RaisePropertyChanged(nameof(ShouldRightCaptureBeVisible));
            RaisePropertyChanged(nameof(ShouldLineGuidesBeVisible));
            RaisePropertyChanged(nameof(ShouldPairPreviewBeVisible));
            RaisePropertyChanged(nameof(ShouldDonutGuideBeVisible));
            RaisePropertyChanged(nameof(ShouldLeftLoadBeVisible));
            RaisePropertyChanged(nameof(ShouldCenterLoadBeVisible));
            RaisePropertyChanged(nameof(ShouldRightLoadBeVisible));
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
            TriggerMovementHint();

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
            RaisePropertyChanged(nameof(CanvasRectangle));
            RaisePropertyChanged(nameof(CanvasRectangleFlags));
            RaisePropertyChanged(nameof(ShouldLeftPairBeVisible));
            RaisePropertyChanged(nameof(ShouldRightPairBeVisible));
            RaisePropertyChanged(nameof(CameraViewModel));
            RaisePropertyChanged(nameof(Settings)); // this doesn't cause reevaluation for above stuff (but I'd like it to), but it does trigger redraw of canvas and evaluation of whether to run auto alignment
            Settings.RaisePropertyChanged();

            if ((((Settings.Mode == DrawMode.Cross || Settings.Mode == DrawMode.RedCyanAnaglyph || Settings.Mode == DrawMode.GrayscaleRedCyanAnaglyph) && !WasCaptureCross) ||
                (Settings.Mode == DrawMode.Parallel && WasCaptureCross)) && LeftBitmap != null && RightBitmap != null)
            {
                SwapSidesCommand.Execute(true);
                WasCaptureCross = !WasCaptureCross;
            }

            BluetoothOperator.CurrentCoreMethods = CoreMethods;
            BluetoothOperator.ErrorOccurred += BluetoothOperatorOnErrorOccurred;

            await Task.Delay(100);
            await EvaluateAndShowWelcomePopup();
        }

        private async void ShowFovPreparationPopup()
        {
            if (!Settings.IsFovCorrectionSet)
            {
                await CoreMethods.DisplayAlert("Field of View Correction",
                    "Different device models can have different fields of view. CrossCam will help you correct for this after you do your first capture. Frame up and capture something with a distinctive points near the top and bottom of the frame, making sure the points are visible on both devices.",
                    "OK");
            }
        }

        private void BluetoothOperatorOnErrorOccurred(object sender, ErrorEventArgs e)
        {
            _secondaryErrorOccurred = true;
        }

        private void BluetoothOperatorOnCapturedImageReceived(object sender, byte[] e)
        {
            PreviewFrame = null;
            var bitmap = DecodeBitmapAndCorrectOrientation(e);
            if (Settings.IsCaptureLeftFirst)
            {
                SetRightBitmap(bitmap, false, true);
            }
            else
            {
                SetLeftBitmap(bitmap, false, true);
            }

            BluetoothOperator.SendTransmissionComplete();
        }

        private void TriggerMovementHint()
        {
            if (LeftBitmap == null &&
                RightBitmap != null ||
                LeftBitmap != null &&
                RightBitmap == null ||
                BluetoothOperator.PairStatus == PairStatus.Connected)
            {
                MoveHintTrigger = !MoveHintTrigger;
            }
        }

        private async Task DrawAnaglyph(bool grayscale)
        {
            var baseWidth = Math.Min(LeftBitmap.Width, RightBitmap.Width);
            var canvasWidth = (int)(baseWidth - baseWidth * (Edits.LeftCrop + Edits.InsideCrop + Edits.OutsideCrop + Edits.RightCrop));
            var canvasHeight = DrawTool.CalculateCanvasHeightLessBorder(LeftBitmap, RightBitmap, Edits);
            using (var tempSurface =
                SKSurface.Create(new SKImageInfo(canvasWidth, canvasHeight)))
            {
                var canvas = tempSurface.Canvas;
                canvas.Clear(SKColor.Empty);

                DrawTool.DrawImagesOnCanvas(
                    canvas, LeftBitmap, RightBitmap, 
                    Settings, 
                    Edits,
                    grayscale ? DrawMode.GrayscaleRedCyanAnaglyph : DrawMode.RedCyanAnaglyph);

                await SaveSurfaceSnapshot(tempSurface);
            }
        }

        protected override void ViewIsDisappearing(object sender, EventArgs e)
        {
            BluetoothOperator.ErrorOccurred -= BluetoothOperatorOnErrorOccurred;

            base.ViewIsDisappearing(sender, e);
        }

        private void BluetoothOperatorOnPreviewFrameReceived(object sender, byte[] bytes)
        {
            PreviewFrame = bytes;
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
                var leftHalf = await Task.Run(() => GetHalfOfFullStereoImage(image, true, Settings.ClipBorderOnNextLoad));
                SetLeftBitmap(leftHalf, false, true);
                var rightHalf = await Task.Run(() => GetHalfOfFullStereoImage(image, false, Settings.ClipBorderOnNextLoad));
                SetRightBitmap(rightHalf, false, true);
                if (Settings.ClipBorderOnNextLoad)
                {
                    Settings.ClipBorderOnNextLoad = false;
                    PersistentStorage.Save(PersistentStorage.SETTINGS_KEY, Settings);
                }
            }
            catch (Exception e)
            {
                ErrorMessage = e.ToString();
            }
        }

        private async Task LeftBytesCaptured(byte[] capturedBytes, bool withMovementAndStep = true)
        {
            var bitmap = await Task.Run(() => DecodeBitmapAndCorrectOrientation(capturedBytes));
            SetLeftBitmap(bitmap, withMovementAndStep, withMovementAndStep);
        }

        private async Task RightBytesCaptured(byte[] capturedBytes, bool withMovementAndStep = true)
        {
            var bitmap = await Task.Run(() => DecodeBitmapAndCorrectOrientation(capturedBytes));
            SetRightBitmap(bitmap, withMovementAndStep, withMovementAndStep);
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
                    var firstImage = Settings.IsCaptureLeftFirst ? LeftBitmap : RightBitmap;
                    var secondImage = Settings.IsCaptureLeftFirst ? RightBitmap : LeftBitmap;
                    try
                    {
                        await Task.Run(() =>
                        {
                            var discardTransX = !Settings.SaveForRedCyanAnaglyph &&
                                                !Settings.AlignHorizontallySideBySide;
                            if (Settings.AlignmentUseKeypoints)
                            {
                                var needsFallback = false;
                                try
                                {
                                    alignedResult = openCv.CreateAlignedSecondImageKeypoints(firstImage, secondImage,
                                        discardTransX, Settings.AlignmentUseCrossCheck, Settings.AlignmentDrawMatches, Settings.AlignmentMinimumKeypoints);
                                    if (alignedResult == null)
                                    {
                                        needsFallback = true;
                                    }
                                }
                                catch (Exception e)
                                {
                                    ErrorMessage = e.ToString();
                                    needsFallback = true;
                                }
                                if (needsFallback)
                                {
                                    alignedResult = openCv.CreateAlignedSecondImageEcc(
                                        firstImage,
                                        secondImage,
                                        Settings.AlignmentDownsizePercentage2,
                                        Settings.AlignmentIterations2,
                                        Settings.AlignmentEpsilonLevel2,
                                        Settings.AlignmentEccThresholdPercentage2,
                                        Settings.AlignmentPyramidLayers2,
                                        discardTransX);
                                }
                            }
                            else
                            {
                                alignedResult = openCv.CreateAlignedSecondImageEcc(
                                    firstImage,
                                    secondImage,
                                    Settings.AlignmentDownsizePercentage2,
                                    Settings.AlignmentIterations2,
                                    Settings.AlignmentEpsilonLevel2,
                                    Settings.AlignmentEccThresholdPercentage2,
                                    Settings.AlignmentPyramidLayers2,
                                    discardTransX);
                            }
                        });
                    }
                    catch (Exception e)
                    {
                        ErrorMessage = e.ToString();
                    }

                    if (alignedResult != null)
                    {
                        if (Settings.AlignmentUseKeypoints && Settings.AlignmentDrawMatches)
                        {
                            using (var tempSurface =
                                SKSurface.Create(new SKImageInfo(alignedResult.DrawnDirtyMatches.Width, alignedResult.DrawnDirtyMatches.Height)))
                            {
                                var canvas = tempSurface.Canvas;
                                canvas.Clear();
                                if (Device.RuntimePlatform == Device.iOS && IsViewInverted)
                                {
                                    canvas.RotateDegrees(180);
                                    canvas.Translate(-1f * alignedResult.DrawnDirtyMatches.Width, -1f * alignedResult.DrawnDirtyMatches.Height);
                                }

                                canvas.DrawBitmap(alignedResult.DrawnDirtyMatches, 0, 0);

                                await SaveSurfaceSnapshot(tempSurface);
                            }

                            using (var tempSurface =
                                SKSurface.Create(new SKImageInfo(alignedResult.DrawnCleanMatches.Width, alignedResult.DrawnCleanMatches.Height)))
                            {
                                var canvas = tempSurface.Canvas;
                                canvas.Clear();
                                if (Device.RuntimePlatform == Device.iOS && IsViewInverted)
                                {
                                    canvas.RotateDegrees(180);
                                    canvas.Translate(-1f * alignedResult.DrawnCleanMatches.Width, -1f * alignedResult.DrawnCleanMatches.Height);
                                }

                                canvas.DrawBitmap(alignedResult.DrawnCleanMatches,0,0);

                                await SaveSurfaceSnapshot(tempSurface);
                            }
                        }

                        _wasAlignmentWithHorizontalRun = Settings.SaveForRedCyanAnaglyph || Settings.AlignHorizontallySideBySide;
                        _wasAlignmentWithoutHorizontalRun = !Settings.SaveForRedCyanAnaglyph && !Settings.AlignHorizontallySideBySide;

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
                                Edits.TopCrop = topLeft.Y / secondImage.Height;
                            }
                        }
                        else
                        {
                            if (topRight.Y > 0)
                            {
                                Edits.TopCrop = topRight.Y / secondImage.Height;
                            }
                        }

                        var maxY = alignedResult.AlignedBitmap.Height - 1;
                        if (bottomLeft.Y < bottomRight.Y)
                        {
                            if (bottomLeft.Y < maxY)
                            {
                                Edits.BottomCrop = (maxY - bottomLeft.Y) / secondImage.Height;
                            }
                        }
                        else
                        {
                            if (bottomRight.Y < maxY)
                            {
                                Edits.BottomCrop = (maxY - bottomRight.Y) / secondImage.Height;
                            }
                        }

                        if (topLeft.X > bottomLeft.X)
                        {
                            if (topLeft.X > 0)
                            {
                                Edits.LeftCrop = topLeft.X / secondImage.Width;
                            }
                        }
                        else
                        {
                            if (bottomLeft.X > 0)
                            {
                                Edits.LeftCrop = bottomLeft.X / secondImage.Width;
                            }
                        }

                        var maxX = alignedResult.AlignedBitmap.Width - 1;
                        if (topRight.X < bottomRight.X)
                        {
                            if (topRight.X < maxX)
                            {
                                Edits.RightCrop = (maxX - topRight.X) / secondImage.Width;
                            }
                        }
                        else
                        {
                            if (bottomRight.X < maxX)
                            {
                                Edits.RightCrop = (maxX - bottomRight.X) / secondImage.Width;
                            }
                        }

                        if (Settings.IsCaptureLeftFirst)
                        {
                            _originalUnalignedBitmap = RightBitmap;
                            SetRightBitmap(alignedResult.AlignedBitmap, false, true);
                        }
                        else
                        {
                            _originalUnalignedBitmap = LeftBitmap;
                            SetLeftBitmap(alignedResult.AlignedBitmap, false, true);
                        }
                    }
                    else
                    {
                        ApplyFovCorrectionToZoom();
                        AlignmentFailFadeTrigger = !AlignmentFailFadeTrigger;
                    }
                }
                else
                {
                    ApplyFovCorrectionToZoom();
                    AutomaticAlignmentNotSupportedTrigger = !AutomaticAlignmentNotSupportedTrigger;
                }

                WorkflowStage = WorkflowStage.Final;

                _alignmentThreadLock = 0;
            }
        }

        private void ApplyFovCorrectionToZoom()
        {
            if (BluetoothOperator.IsPrimary &&
                BluetoothOperator.PairStatus == PairStatus.Connected)
            {
                if (Settings.IsCaptureLeftFirst)
                {
                    Edits.FovLeftCorrection = Settings.FovPrimaryCorrection;
                    Edits.FovRightCorrection = Settings.FovSecondaryCorrection;
                }
                else
                {
                    Edits.FovRightCorrection = Settings.FovPrimaryCorrection;
                    Edits.FovLeftCorrection = Settings.FovSecondaryCorrection;
                }
            }
        }

        public void SetLeftBitmap(SKBitmap bitmap, bool withMovementTrigger, bool stepForward)
        {
            if (bitmap == null) return;

            LeftBitmap = bitmap;
            WasCapturePortrait = LeftBitmap.Width < LeftBitmap.Height;

            if (stepForward)
            {
                if (RightBitmap == null)
                {
                    if (withMovementTrigger)
                    {
                        TriggerMovementHint();
                    }
                    CameraColumn = 1;
                    WorkflowStage = WorkflowStage.Capture;
                }
                else
                {
                    CheckAndCorrectResolutionDifferences();
                    if (BluetoothOperator.IsPrimary &&
                        BluetoothOperator.PairStatus == PairStatus.Connected)
                    {
                        if (!Settings.IsFovCorrectionSet)
                        {
                            WorkflowStage = WorkflowStage.FovCorrection; //TODO: pop an explanation popup, give some better wording on the pair page
                            ShowFovDialog();
                            return;
                        }
                    }
                    WasCaptureCross = Settings.Mode != DrawMode.Parallel;
                    CameraColumn = Settings.IsCaptureLeftFirst ? 0 : 1;
                    WorkflowStage = WorkflowStage.Final;
                    AutoAlign();
                }
            }
        }

        public void SetRightBitmap(SKBitmap bitmap, bool withMovementTrigger, bool stepForward)
        {
            if (bitmap == null) return;

            RightBitmap = bitmap;
            WasCapturePortrait = RightBitmap.Width < RightBitmap.Height;

            if (stepForward)
            {
                if (LeftBitmap == null)
                {
                    if (withMovementTrigger)
                    {
                        TriggerMovementHint();
                    }
                    CameraColumn = 0;
                    WorkflowStage = WorkflowStage.Capture;
                }
                else
                {
                    CheckAndCorrectResolutionDifferences();
                    if (BluetoothOperator.IsPrimary &&
                        BluetoothOperator.PairStatus == PairStatus.Connected)
                    {
                        if (!Settings.IsFovCorrectionSet)
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
                }
            }
        }

        private void CheckAndCorrectResolutionDifferences()
        {
            var leftRatio = LeftBitmap.Width / (1f * LeftBitmap.Height);
            var rightRatio = RightBitmap.Width / (1f * RightBitmap.Height);
            if (leftRatio != rightRatio)// TODO: something
            {

            }
            else
            {
                if (LeftBitmap.Width < RightBitmap.Width)
                {
                    var corrected = new SKBitmap(LeftBitmap.Width, LeftBitmap.Height);
                    using (var surface = new SKCanvas(corrected))
                    {
                        surface.DrawBitmap(RightBitmap,0,0);
                    }
                    RightBitmap = corrected;
                }
                else if (RightBitmap.Width < LeftBitmap.Width)
                {
                    var corrected = new SKBitmap(RightBitmap.Width, RightBitmap.Height);
                    using (var surface = new SKCanvas(corrected))
                    {
                        surface.DrawBitmap(LeftBitmap, 0, 0);
                    }
                    LeftBitmap = corrected;
                }
            }
        }

        private async void ShowFovDialog()
        {
            await CoreMethods.DisplayAlert("Field of View Correction", "To correct for field of view differences, zoom and slide the pictures so the distinctive points line up between the two photos. You can drag the white lines around to help you visualize the alignment. This correction will be applied to future previews. It will be saved but you can reset it on the Settings page.", "OK");
        }

        private static SKBitmap GetHalfOfFullStereoImage(byte[] bytes, bool wantLeft, bool clipBorder) 
        {
            var original = SKBitmap.Decode(bytes);

            if (original == null) return null;

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

        public SKBitmap DecodeBitmapAndCorrectOrientation(byte[] bytes, bool withOrientationByte = false)
        {
            try
            {

                withOrientationByte = withOrientationByte && Device.RuntimePlatform == Device.Android;
                if (withOrientationByte)
                {
                    using (var stream = new MemoryStream(bytes, 0, bytes.Length - 1))
                    {
                        return InternalDecodeAndCorrect(stream, bytes.Last());
                    }
                }

                using (var stream = new MemoryStream(bytes))
                {
                    return InternalDecodeAndCorrect(stream, null);
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

        private SKBitmap InternalDecodeAndCorrect(Stream stream, byte? orientationByte)
        {
            SKCodecOrigin origin = 0;
            using (var data = SKData.Create(stream))
            using (var codec = SKCodec.Create(data))
            {
                var bitmap = SKBitmap.Decode(data);

                if (codec != null)
                {
                    origin = codec.Origin;
                }
                
                if (!orientationByte.HasValue)
                {
                    switch (origin)
                    {
                        case SKCodecOrigin.BottomRight when !ChosenCamera.IsFront:
                            return BitmapRotate180(bitmap, false);
                        case SKCodecOrigin.BottomRight when ChosenCamera.IsFront:
                            return BitmapHorizontalMirror(bitmap);
                        case SKCodecOrigin.RightTop:
                            return BitmapRotate90(bitmap, ChosenCamera.IsFront);
                        case SKCodecOrigin.LeftBottom:
                            return BitmapRotate270(bitmap, ChosenCamera.IsFront);
                        case SKCodecOrigin.TopLeft when ChosenCamera.IsFront:
                            return BitmapRotate180(bitmap, true);
                        default:
                            return bitmap;
                    }
                }

                switch (orientationByte)
                {
                    case 1:
                        return BitmapRotate90(bitmap, false);
                    case 2:
                        return BitmapRotate180(bitmap, false);
                    case 3:
                        return BitmapRotate270(bitmap, false);
                    default:
                        return bitmap;
                }
            }
        }

        private static SKBitmap BitmapRotate90(SKBitmap originalBitmap, bool withHorizontalMirror)
        {
            var rotated = new SKBitmap(originalBitmap.Height, originalBitmap.Width);

            using (var surface = new SKCanvas(rotated))
            {
                surface.Translate(rotated.Width, 0);
                surface.RotateDegrees(90);
                if (withHorizontalMirror)
                {
                    surface.Translate(0, rotated.Width);
                    surface.Scale(1, -1, 0, 0);
                }
                surface.DrawBitmap(originalBitmap, 0, 0);
            }

            return rotated;
        }

        private static SKBitmap BitmapRotate180(SKBitmap originalBitmap, bool withHorizontalMirror)
        {
            var rotated = new SKBitmap(originalBitmap.Width, originalBitmap.Height);

            using (var surface = new SKCanvas(rotated))
            {
                surface.Translate(rotated.Width, rotated.Height);
                surface.RotateDegrees(180);
                if (withHorizontalMirror)
                {
                    surface.Translate(rotated.Width, 0);
                    surface.Scale(-1, 1, 0, 0);
                }
                surface.DrawBitmap(originalBitmap, 0, 0);
            }

            return rotated;
        }

        private static SKBitmap BitmapRotate270(SKBitmap originalBitmap, bool withHorizontalMirror)
        {
            var rotated = new SKBitmap(originalBitmap.Height, originalBitmap.Width);

            using (var surface = new SKCanvas(rotated))
            {
                surface.Translate(0, rotated.Height);
                surface.RotateDegrees(270);
                if (withHorizontalMirror)
                {
                    surface.Translate(0, rotated.Width);
                    surface.Scale(1, -1, 0, 0);
                }
                surface.DrawBitmap(originalBitmap, 0, 0);
            }

            return rotated;
        }

        private static SKBitmap BitmapHorizontalMirror(SKBitmap originalBitmap)
        {
            var transformed = new SKBitmap(originalBitmap.Width, originalBitmap.Height);

            using (var surface = new SKCanvas(transformed))
            {
                surface.Translate(transformed.Width, 0);
                surface.Scale(-1, 1, 0, 0);
                surface.DrawBitmap(originalBitmap, 0, 0);
            }

            return transformed;
        }

        private async Task EvaluateAndShowWelcomePopup()
        {
#if DEBUG
#else
            if (!Settings.HasOfferedTechniqueHelpBefore)
            {
                var showTechniquePage = await CoreMethods.DisplayAlert("Welcome to CrossCam!",
                    "CrossCam was made to help you take 3D photos. The photos are 3D just like VR or 3D movies, but you don't need any special equipment or glasses - just your phone. The technique to view the 3D photos is a little tricky and takes some practice to get it right. Before I tell you how to use CrossCam, would you first like to learn more about the viewing technique?",
                    "Yes", "No");
                Settings.HasOfferedTechniqueHelpBefore = true;
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
#endif
        }

        private void ClearCrops(bool andAutomaticAlignmentFlags)
        {
            Edits.OutsideCrop = 0;
            Edits.InsideCrop = 0;
            Edits.RightCrop = 0;
            Edits.LeftCrop = 0;
            Edits.TopCrop = 0;
            Edits.BottomCrop = 0;
            CropMode = CropMode.Inside;
            if (andAutomaticAlignmentFlags)
            {
                _wasAlignmentWithoutHorizontalRun = false;
                _wasAlignmentWithHorizontalRun = false;
            }
        }

        private void ClearAlignments()
        {
            Edits.LeftRotation = 0;
            Edits.RightRotation = 0;
            Edits.LeftZoom = 0;
            Edits.RightZoom = 0;
            Edits.VerticalAlignment = 0;
            ManualAlignMode = ManualAlignMode.VerticalAlign;
        }

        private void ClearKeystone()
        {
            Edits.LeftKeystone = 0;
            Edits.RightKeystone = 0;
            KeystoneMode = KeystoneMode.Left;
        }

        private void ClearEdits()
        {
            if (_originalUnalignedBitmap != null)
            {
                if (Settings.IsCaptureLeftFirst)
                {
                    RightBitmap = _originalUnalignedBitmap;
                }
                else
                {
                    LeftBitmap = _originalUnalignedBitmap;
                }
            }
            ClearCrops(true);
            ClearAlignments();
            ClearKeystone();
        }

        private void ClearCaptures()
        {
            if (BluetoothOperator.PairStatus == PairStatus.Connected &&
                BluetoothOperator.IsPrimary)
            {
                BluetoothOperator.RequestPreviewFrame();
            }
            ClearEdits();
            LeftBitmap?.Dispose();
            LeftBitmap = null;
            RightBitmap?.Dispose();
            RightBitmap = null;
            _originalUnalignedBitmap?.Dispose();
            _originalUnalignedBitmap = null;
            _secondaryErrorOccurred = false;
            WorkflowStage = WorkflowStage.Capture;

            if (Settings.IsTapToFocusEnabled2)
            {
                SwitchToContinuousFocusTrigger = !SwitchToContinuousFocusTrigger;
            }
        }
    }
}