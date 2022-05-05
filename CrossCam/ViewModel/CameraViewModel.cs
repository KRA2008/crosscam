﻿using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
        public CameraSettingMode CameraSettingMode { get; set; }
        public bool IsCameraSelectVisible { get; set; }
        public FovCorrectionMode FovCorrectionMode { get; set; }

        public SKBitmap LeftBitmap { get; set; }
        public SKMatrix LeftAlignmentTransform { get; set; }
        public SKEncodedOrigin LeftOrientation { get; set; }
        public bool IsLeftFrontFacing { get; set; }
        public Command RetakeLeftCommand { get; set; }
        public bool LeftCaptureSuccess { get; set; }
        
        public SKBitmap RightBitmap { get; set; }
        public SKMatrix RightAlignmentTransform { get; set; }
        public SKEncodedOrigin RightOrientation { get; set; }
        public bool IsRightFrontFacing { get; set; }
        public Command RetakeRightCommand { get; set; }
        public bool RightCaptureSuccess { get; set; }

        public bool CaptureSuccess { get; set; }
        public int CameraColumn { get; set; }

        public byte[] RemotePreviewFrame { get; set; }
        public IncomingFrame LocalPreviewFrame { get; set; }
        public IncomingFrame LocalCapturedFrame { get; set; }

        public AbsoluteLayoutFlags CanvasRectangleFlags => 
            Settings.Mode == DrawMode.Parallel && 
            !(WorkflowStage == WorkflowStage.Capture && 
              Settings.ShowGhostCaptures)
            ? AbsoluteLayoutFlags.YProportional | AbsoluteLayoutFlags.HeightProportional | AbsoluteLayoutFlags.XProportional : 
            AbsoluteLayoutFlags.All;
        public Rectangle CanvasRectangle 
        {
            get 
            {
                if(Settings.Mode != DrawMode.Parallel ||
                   WorkflowStage == WorkflowStage.Capture &&
                   Settings.ShowGhostCaptures) return new Rectangle(0,0,1,1);
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

        public double PreviewAspectRatio { get; set; }

        public Command CapturePictureCommand { get; set; }
        public bool CapturePictureTrigger { get; set; }

        public bool SingleMoveHintTrigger { get; set; }
        public bool DoubleMoveHintTrigger { get; set; }
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

        public Command FlipCameraCommand { get; set; }

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

        public float MaxKeystone => 15f;

        public Command LoadPhotoCommand { get; set; }

        public bool IsViewPortrait { get; set; }
        public bool IsViewInverted { get; set; }
        public bool WasCapturePortrait { get; set; }
        public bool WasCaptureCross { get; set; }
        public bool WasCapturePaired { get; set; }

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

        private bool _isClearPromptOpen;

        public Rectangle PairButtonPosition
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
                return new Rectangle(x, 1, width, height);
            }
        }

        public bool ShouldLeftLeftRetakeBeVisible => LeftBitmap != null && 
                                                     (WorkflowStage == WorkflowStage.Final && 
                                                      DoesCaptureOrientationMatchViewOrientation || 
                                                      WorkflowStage == WorkflowStage.Capture && 
                                                      (Settings.PortraitCaptureButtonPosition == PortraitCaptureButtonPosition.Right || 
                                                       Settings.PortraitCaptureButtonPosition == PortraitCaptureButtonPosition.Middle));
        public bool ShouldLeftRightRetakeBeVisible => LeftBitmap != null && 
                                                      WorkflowStage == WorkflowStage.Capture && 
                                                      Settings.PortraitCaptureButtonPosition == PortraitCaptureButtonPosition.Left;
        public bool ShouldRightLeftRetakeBeVisible => RightBitmap != null && 
                                                      WorkflowStage == WorkflowStage.Capture && 
                                                      Settings.PortraitCaptureButtonPosition == PortraitCaptureButtonPosition.Right;
        public bool ShouldRightRightRetakeBeVisible => RightBitmap != null && 
                                                       (WorkflowStage == WorkflowStage.Final && 
                                                        DoesCaptureOrientationMatchViewOrientation || 
                                                        WorkflowStage == WorkflowStage.Capture && 
                                                        (Settings.PortraitCaptureButtonPosition == PortraitCaptureButtonPosition.Left || 
                                                         Settings.PortraitCaptureButtonPosition == PortraitCaptureButtonPosition.Middle));
        
        public bool ShouldCenterLoadBeVisible => WorkflowStage == WorkflowStage.Capture && 
                                                 Settings.PortraitCaptureButtonPosition != PortraitCaptureButtonPosition.Middle && 
                                                 BluetoothOperator.PairStatus != PairStatus.Connected;
        public bool ShouldLeftLoadBeVisible => CameraColumn == 0 && 
                                               WorkflowStage == WorkflowStage.Capture && 
                                               Settings.PortraitCaptureButtonPosition == PortraitCaptureButtonPosition.Middle && 
                                               BluetoothOperator.PairStatus != PairStatus.Connected;
        public bool ShouldRightLoadBeVisible => CameraColumn == 1 && 
                                                WorkflowStage == WorkflowStage.Capture && 
                                                Settings.PortraitCaptureButtonPosition == PortraitCaptureButtonPosition.Middle && 
                                                BluetoothOperator.PairStatus != PairStatus.Connected;
        
        public bool DoesCaptureOrientationMatchViewOrientation => WasCapturePortrait == IsViewPortrait;
        public bool ShouldSettingsAndHelpBeVisible => !IsBusy && 
                                                      WorkflowStage != WorkflowStage.View;
        public bool IsExactlyOnePictureTaken => LeftBitmap == null ^ RightBitmap == null;
        public bool IsCaptureModeAndEitherPrimaryOrDisconnected => WorkflowStage == WorkflowStage.Capture && 
                                                                    (BluetoothOperatorBindable.PairStatus == PairStatus.Disconnected || 
                                                                     Settings.IsPairedPrimary.HasValue && Settings.IsPairedPrimary.Value);

        public Rectangle CaptureButtonPosition
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
                    return new Rectangle(x, y, width, width);
                }

                return new Rectangle(0.5, 0, width, 4000);
            }
        }

        public bool ShouldLineGuidesBeVisible => (((IsNothingCaptured && Settings.ShowGuideLinesWithFirstCapture)
                                                  || (IsExactlyOnePictureTaken && WorkflowStage != WorkflowStage.Loading)
                                                  || (BluetoothOperator.IsPrimary && BluetoothOperator.PairStatus == PairStatus.Connected && WorkflowStage == WorkflowStage.Capture))
                                                  && Settings.AreGuideLinesVisible
                                                  || WorkflowStage == WorkflowStage.Keystone
                                                  || WorkflowStage == WorkflowStage.ManualAlign
                                                  || WorkflowStage == WorkflowStage.FovCorrection) && 
                                                 Settings.Mode != DrawMode.Cardboard;
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
        public bool IsHoldSteadySecondary { get; set; }
        public bool ShouldSaveCapturesButtonBeVisible => WorkflowStage == WorkflowStage.Final &&
                                                         (Settings.SaveForCrossView ||
                                                          Settings.SaveForParallel ||
                                                          Settings.SaveSidesSeparately ||
                                                          Settings.SaveRedundantFirstSide ||
                                                          Settings.SaveForRedCyanAnaglyph ||
                                                          Settings.SaveForGrayscaleAnaglyph ||
                                                          Settings.SaveForTriple ||
                                                          Settings.SaveForQuad ||
                                                          Settings.SaveForCardboard);

        public bool ShouldPairPreviewBeVisible => (BluetoothOperator.PairStatus == PairStatus.Connected &&
                                                      BluetoothOperator.IsPrimary || Settings.Mode == DrawMode.Cardboard)  &&
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
                                               DrawTool.CalculateJoinedCanvasWidthWithEditsNoBorder(LeftBitmap,
                                                   LeftOrientation, RightBitmap, RightOrientation, Edits) >
                                               DrawTool.CalculateCanvasHeightWithEditsNoBorder(
                                                   LeftBitmap, LeftOrientation, RightBitmap, RightOrientation,
                                                   Edits);

        public string SavedSuccessMessage => "Saved to " + (Settings.SaveToExternal
                                                 ? "external"
                                                 :
                                                 !string.IsNullOrWhiteSpace(Settings.SavingDirectory)
                                                     ?
                                                     "custom folder"
                                                     : "Photos") + "!";

        private WorkflowStage _stageBeforeView;
        private int _alignmentThreadLock;
        private bool _isAlignmentInvalid = true;
        private readonly IPhotoSaver _photoSaver;
        private bool _secondaryErrorOccurred;
        private bool _isFovCorrected;
        private bool _isInitialized;

        public CameraViewModel()
        {
            _photoSaver = DependencyService.Get<IPhotoSaver>();

            Settings = PersistentStorage.LoadOrDefault(PersistentStorage.SETTINGS_KEY, new Settings());
            Edits = new Edits(Settings);
            LeftAlignmentTransform = SKMatrix.Identity;
            RightAlignmentTransform = SKMatrix.Identity;
            Settings.PropertyChanged += SettingsOnPropertyChanged;
            Settings.AlignmentSettings.PropertyChanged += AlignmentSettingsOnPropertyChanged;
            BluetoothOperator = new BluetoothOperator(Settings);
            BluetoothOperator.Connected += BluetoothOperatorOnConnected;
            BluetoothOperator.Disconnected += BluetoothOperatorOnDisconnected;
            BluetoothOperator.PreviewFrameReceived += BluetoothOperatorOnPreviewFrameReceived;
            BluetoothOperator.CapturedImageReceived += BluetoothOperatorOnCapturedImageReceived;
            BluetoothOperator.InitialSyncStarted += BluetoothOperatorInitialSyncStarted;
            BluetoothOperator.InitialSyncCompleted += BluetoothOperatorInitialSyncCompleted;
            BluetoothOperator.TransmissionStarted += BluetoothOperatorTransmissionStarted;
            BluetoothOperator.TransmissionComplete += BluetoothOperatorTransmissionComplete;
            BluetoothOperator.CountdownTimerSyncCompleteSecondary += BluetoothOperatorCountdownTimerSyncCompleteSecondary;

            DeviceDisplay.MainDisplayInfoChanged += (sender, args) =>
            {
                EvaluateOrientation(args.DisplayInfo.Rotation);
            };

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
                else if (args.PropertyName == nameof(LocalCapturedFrame))
                {
                    if (_secondaryErrorOccurred)
                    {
                        ClearCaptures();
                    }
                    else
                    {
                        if (BluetoothOperator.IsPrimary &&
                            BluetoothOperator.PairStatus == PairStatus.Connected)
                        {
                            WasCapturePaired = true;
                            WorkflowStage = WorkflowStage.Loading;
                            RaisePropertyChanged(nameof(ShouldLineGuidesBeVisible));
                            RaisePropertyChanged(nameof(ShouldDonutGuideBeVisible));
                        }
                        else
                        {
                            WasCapturePaired = false;
                        }

                        if (CameraColumn == 0)
                        {
                            SetLeftBitmap(
                                LocalCapturedFrame.Frame, LocalCapturedFrame.Orientation, LocalCapturedFrame.IsFrontFacing,
                                BluetoothOperator.PairStatus == PairStatus.Disconnected,
                                BluetoothOperator.PairStatus == PairStatus.Disconnected);
                        }
                        else
                        {
                            SetRightBitmap(
                                LocalCapturedFrame.Frame, LocalCapturedFrame.Orientation, LocalCapturedFrame.IsFrontFacing,
                                BluetoothOperator.PairStatus == PairStatus.Disconnected,
                                BluetoothOperator.PairStatus == PairStatus.Disconnected);
                        }
                    }
                }
                else if (args.PropertyName == nameof(ErrorMessage))
                {
                    if (ErrorMessage != null &&
                        Settings.SendErrorReports1)
                    {
                        PromptForPermissionAndSendErrorEmailCommand.Execute(null);
                    }
                }
                else if (args.PropertyName == nameof(Settings))
                {
                    if (_isAlignmentInvalid)
                    {
                        ClearCrops(true);
                        if (Settings.IsCaptureLeftFirst)
                        {
                            SetRightBitmap(RightBitmap, RightOrientation, IsRightFrontFacing, true, true); //calls autoalign internally
                        }
                        else
                        {
                            SetLeftBitmap(LeftBitmap, LeftOrientation, IsLeftFrontFacing, true, true); //calls autoalign internally
                        }
                    }

                    AutoAlign();
                }
                else if (args.PropertyName == nameof(WasSwipedTrigger))
                {
                    SwapSidesCommand?.Execute(null);
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
                        if (RightBitmap == null)
                        {
                            ClearCaptures();
                        }
                        else
                        {
                            LeftBitmap = null;
                            ClearEdits(true);
                            CameraColumn = 0;
                            TriggerMovementHint();
                            WorkflowStage = WorkflowStage.Capture;
                        }
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
                        if (LeftBitmap == null)
                        {
                            ClearCaptures();
                        }
                        else
                        {
                            RightBitmap = null;
                            ClearEdits(true);
                            CameraColumn = 1;
                            TriggerMovementHint();
                            WorkflowStage = WorkflowStage.Capture;
                        }
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
                    await Device.InvokeOnMainThreadAsync(async () =>
                    {
                        if (!_isClearPromptOpen)
                        {
                            _isClearPromptOpen = true;
                            var confirmClear = await CoreMethods.DisplayAlert("Really clear?",
                                "Are you sure you want to clear your pictures and start over?", "Yes, clear", "No");
                            if (confirmClear)
                            {
                                ClearCaptures();
                            }
                            _isClearPromptOpen = false;
                        }
                    });
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

            FlipCameraCommand = new Command(() =>
            {
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
                if (WorkflowStage == WorkflowStage.Capture ||
                    WorkflowStage == WorkflowStage.Final ||
                    WorkflowStage == WorkflowStage.Edits ||
                    obj is bool forced && forced)
                {
                    (LeftBitmap, RightBitmap) = (RightBitmap, LeftBitmap);
                    (LeftAlignmentTransform, RightAlignmentTransform) = (RightAlignmentTransform, LeftAlignmentTransform);
                    (LeftOrientation, RightOrientation) = (RightOrientation, LeftOrientation);
                    (IsLeftFrontFacing, IsRightFrontFacing) = (IsRightFrontFacing, IsLeftFrontFacing);

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
                    Settings.RaisePropertyChanged(nameof(Settings.IsCaptureLeftFirst));
                    RaisePropertyChanged(nameof(PairButtonPosition));
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
                            var leftWidth = LeftBitmap.Width;
                            var leftHeight = LeftBitmap.Height;
                            if (DrawTool.Orientations90deg.Contains(LeftOrientation))
                            {
                                (leftWidth, leftHeight) = (leftHeight, leftWidth);
                            }

                            using var tempSurface =
                                SKSurface.Create(new SKImageInfo(leftWidth, leftHeight));
                            using var canvas = tempSurface.Canvas;

                            canvas.Clear();
                            if (needs180Flip)
                            {
                                canvas.RotateDegrees(180);
                                canvas.Translate(-1f * leftWidth, -1f * leftHeight);
                            }

                            var leftMatrix = DrawTool.FindOrientationMatrix(LeftOrientation, leftWidth / 2f,
                                leftHeight / 2f, IsLeftFrontFacing);
                            canvas.SetMatrix(leftMatrix);
                            var leftDestRect = leftMatrix.Invert().MapRect(SKRect.Create(0, 0, leftWidth, leftHeight));
                            canvas.DrawBitmap(LeftBitmap, leftDestRect);

                            await SaveSurfaceSnapshot(tempSurface, "Separate");

                            canvas.Clear();
                            canvas.ResetMatrix();

                            var rightWidth = RightBitmap.Width;
                            var rightHeight = RightBitmap.Height;
                            if (DrawTool.Orientations90deg.Contains(RightOrientation))
                            {
                                (rightWidth, rightHeight) = (rightHeight, rightWidth);
                            }
                            if (needs180Flip)
                            {
                                canvas.RotateDegrees(180);
                                canvas.Translate(-1f * rightWidth, -1f * rightHeight);
                            }

                            var rightMatrix = DrawTool.FindOrientationMatrix(RightOrientation, rightWidth / 2f,
                                rightHeight / 2f, IsRightFrontFacing);
                            canvas.SetMatrix(rightMatrix);
                            var rightDestRect = rightMatrix.Invert()
                                .MapRect(SKRect.Create(0, 0, rightWidth, rightHeight));
                            canvas.DrawBitmap(RightBitmap, rightDestRect);

                            await SaveSurfaceSnapshot(tempSurface, "Separate");
                        }

                        var finalImageWidth = DrawTool.CalculateJoinedCanvasWidthWithEditsNoBorder(LeftBitmap,
                            LeftOrientation, RightBitmap, RightOrientation, Edits);
                        var finalTripleWidth = (int) (1.5 * finalImageWidth);
                        var borderThickness = Settings.AddBorder
                            ? (int) (DrawTool.BORDER_CONVERSION_FACTOR * Settings.BorderWidthProportion *
                                     finalImageWidth)
                            : 0;
                        finalImageWidth += (3 * borderThickness);
                        finalTripleWidth += (int)(4.25d * borderThickness); //I don't know why 1/4 but that's what it is.
                        var tripleHalfOffset = (int) (finalImageWidth - borderThickness / 2d); //I don't know why 1/2 but that's what it is.
                        var finalImageHeight = DrawTool.CalculateCanvasHeightWithEditsNoBorder(LeftBitmap,
                                                   LeftOrientation, RightBitmap, RightOrientation, Edits) +
                                               2 * borderThickness;
                        if (Settings.SaveWithFuseGuide)
                        {
                            finalImageHeight += (int)DrawTool.CalculateFuseGuideMarginHeight(finalImageHeight);
                        }
                        var quadHeight = (int) (finalImageHeight * 2 - borderThickness /2d); //I don't know why 1/2 but that's what it is.
                        var quadOffset = (int) (finalImageHeight - borderThickness / 2d); //I don't know why 1/2 but that's what it is.

                        tripleHalfOffset = (int) (tripleHalfOffset * (Settings.ResolutionProportion / 100d));
                        finalTripleWidth = (int) (finalTripleWidth * (Settings.ResolutionProportion / 100d));
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
                            using var tempSurface =
                                SKSurface.Create(new SKImageInfo(finalImageWidth, finalImageHeight));
                            using var canvas = tempSurface.Canvas;
                            canvas.Clear();
                            if (needs180Flip)
                            {
                                canvas.RotateDegrees(180);
                                canvas.Translate(-1f * finalImageWidth, -1f * finalImageHeight);
                            }

                            DrawTool.DrawImagesOnCanvas(
                                tempSurface, 
                                LeftBitmap, LeftAlignmentTransform, LeftOrientation, IsLeftFrontFacing,
                                RightBitmap, RightAlignmentTransform, RightOrientation, IsRightFrontFacing,
                                Settings,
                                Edits, 
                                DrawMode.Cross);

                            await SaveSurfaceSnapshot(tempSurface,
                                Settings.Mode == DrawMode.Parallel ? "Parallel" : "Cross");
                        }

                        if (Settings.SaveForParallel &&
                            Settings.Mode == DrawMode.Cross ||
                            Settings.SaveForCrossView &&
                            Settings.Mode == DrawMode.Parallel)
                        {
                            using var tempSurface =
                                SKSurface.Create(new SKImageInfo(finalImageWidth, finalImageHeight));
                            using var canvas = tempSurface.Canvas;
                            canvas.Clear();
                            if (needs180Flip)
                            {
                                canvas.RotateDegrees(180);
                                canvas.Translate(-1f * finalImageWidth, -1f * finalImageHeight);
                            }

                            DrawTool.DrawImagesOnCanvas(
                                tempSurface, 
                                LeftBitmap, LeftAlignmentTransform, LeftOrientation, IsLeftFrontFacing,
                                RightBitmap, RightAlignmentTransform, RightOrientation, IsRightFrontFacing,
                                Settings, 
                                Edits, 
                                DrawMode.Parallel,
                                withSwap: true);

                            await SaveSurfaceSnapshot(tempSurface,
                                Settings.Mode == DrawMode.Cross ? "Parallel" : "Cross");
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
                            SKBitmap targetBitmap;
                            SKEncodedOrigin targetOrientation;
                            bool targetFront;
                            if (Settings.IsCaptureLeftFirst)
                            {
                                targetBitmap = LeftBitmap;
                                targetOrientation = LeftOrientation;
                                targetFront = IsLeftFrontFacing;
                            }
                            else
                            {
                                targetBitmap = RightBitmap;
                                targetOrientation = RightOrientation;
                                targetFront = IsRightFrontFacing;
                            }

                            var width = targetBitmap.Width;
                            var height = targetBitmap.Height;
                            if (DrawTool.Orientations90deg.Contains(targetOrientation))
                            {
                                (width, height) = (height, width);
                            }

                            using var tempSurface =
                                SKSurface.Create(new SKImageInfo(width, height));
                            using var canvas = tempSurface.Canvas;
                            canvas.Clear();
                            if (needs180Flip)
                            {
                                canvas.RotateDegrees(180);
                                canvas.Translate(-1f * width, -1f * height);
                            }

                            var orientationMatrix = DrawTool.FindOrientationMatrix(targetOrientation, width / 2f,
                                height / 2f, targetFront);
                            canvas.SetMatrix(orientationMatrix);
                            var destRect = orientationMatrix.Invert().MapRect(SKRect.Create(0, 0, width, height));
                            canvas.DrawBitmap(targetBitmap, destRect);

                            await SaveSurfaceSnapshot(tempSurface, "Single");
                        }

                        if (Settings.SaveForTriple)
                        {
                            using var doubleSurface =
                                SKSurface.Create(new SKImageInfo(finalImageWidth, finalImageHeight));
                            using var doubleCanvas = doubleSurface.Canvas;
                            doubleCanvas.Clear();
                            if (needs180Flip)
                            {
                                doubleCanvas.RotateDegrees(180);
                                doubleCanvas.Translate(-1f * finalImageWidth, -1f * finalImageHeight);
                            }

                            DrawTool.DrawImagesOnCanvas(
                                doubleSurface, 
                                LeftBitmap, LeftAlignmentTransform, LeftOrientation, IsLeftFrontFacing,
                                RightBitmap, RightAlignmentTransform, RightOrientation, IsRightFrontFacing,
                                Settings,
                                Edits,
                                DrawMode.Cross);

                            using var tripleSurface =
                                SKSurface.Create(new SKImageInfo(finalTripleWidth, finalImageHeight));
                            using var tripleCanvas = tripleSurface.Canvas;
                            tripleCanvas.Clear();

                            tripleCanvas.DrawSurface(doubleSurface, 0, 0);
                            tripleCanvas.DrawSurface(doubleSurface, tripleHalfOffset, 0);

                            await SaveSurfaceSnapshot(tripleSurface, "Triple");
                        }

                        if (Settings.SaveForQuad)
                        {
                            using var doublePlainSurface =
                                SKSurface.Create(new SKImageInfo(finalImageWidth, finalImageHeight));
                            using var doublePlainCanvas = doublePlainSurface.Canvas;
                            doublePlainCanvas.Clear();
                            if (needs180Flip)
                            {
                                doublePlainCanvas.RotateDegrees(180);
                                doublePlainCanvas.Translate(-1f * finalImageWidth, -1f * finalImageHeight);
                            }

                            DrawTool.DrawImagesOnCanvas(
                                doublePlainSurface, 
                                LeftBitmap, LeftAlignmentTransform, LeftOrientation, IsLeftFrontFacing,
                                RightBitmap, RightAlignmentTransform, RightOrientation, IsRightFrontFacing,
                                Settings,
                                Edits,
                                DrawMode.Cross);

                            using var doubleSwapSurface =
                                SKSurface.Create(new SKImageInfo(finalImageWidth, finalImageHeight));
                            using var doubleSwapCanvas = doubleSwapSurface.Canvas;
                            doubleSwapCanvas.Clear();
                            if (needs180Flip)
                            {
                                doubleSwapCanvas.RotateDegrees(180);
                                doubleSwapCanvas.Translate(-1f * finalImageWidth, -1f * finalImageHeight);
                            }

                            DrawTool.DrawImagesOnCanvas(
                                doubleSwapSurface, 
                                LeftBitmap, LeftAlignmentTransform, LeftOrientation, IsLeftFrontFacing,
                                RightBitmap, RightAlignmentTransform, RightOrientation, IsRightFrontFacing,
                                Settings,
                                Edits,
                                DrawMode.Cross, withSwap: true);

                            using var quadSurface =
                                SKSurface.Create(new SKImageInfo(finalImageWidth, quadHeight));
                            using var quadCanvas = quadSurface.Canvas;
                            quadCanvas.Clear();

                            quadCanvas.DrawSurface(doublePlainSurface, 0, 0);
                            quadCanvas.DrawSurface(doubleSwapSurface, 0, quadOffset);

                            await SaveSurfaceSnapshot(quadSurface, "Quad");
                        }

                        if (Settings.SaveForCardboard)
                        {
                            var width = DrawTool.CalculateJoinedCanvasWidthWithEditsNoBorder(LeftBitmap,
                                LeftOrientation, RightBitmap, RightOrientation,
                                Edits);
                            var height =
                                DrawTool.CalculateCanvasHeightWithEditsNoBorder(LeftBitmap, LeftOrientation,
                                    RightBitmap, RightOrientation, Edits);

                            using var tempSurface = SKSurface.Create(new SKImageInfo(width, height));
                            using var canvas = tempSurface.Canvas;
                            canvas.Clear();

                            var withBorderTemp = Settings.AddBorder;
                            Settings.AddBorder = false;
                            var fuseGuideTemp = Settings.SaveWithFuseGuide;
                            Settings.SaveWithFuseGuide = false;

                            DrawTool.DrawImagesOnCanvas(tempSurface, 
                                LeftBitmap, LeftAlignmentTransform, LeftOrientation, IsLeftFrontFacing,
                                RightBitmap, RightAlignmentTransform, RightOrientation, IsRightFrontFacing,
                                Settings, Edits, DrawMode.Parallel, withSwap:Settings.Mode == DrawMode.Cross ||
                                                                             Settings.Mode == DrawMode.RedCyanAnaglyph ||
                                                                             Settings.Mode == DrawMode.GrayscaleRedCyanAnaglyph);

                            Settings.AddBorder = withBorderTemp;
                            Settings.SaveWithFuseGuide = fuseGuideTemp;

                            await SaveSurfaceSnapshot(tempSurface, "Cardboard");
                        }
                    });
                }
                catch (DirectoryNotFoundException)
                {
                    SaveFailFadeTrigger = !SaveFailFadeTrigger;
                    WorkflowStage = WorkflowStage.Final;

                    await Device.InvokeOnMainThreadAsync(async () =>
                    {
                        await CoreMethods.DisplayAlert("Directory Not Found",
                            "The save destination could not be found. Please choose another on the settings page.",
                            "OK");
                    });

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
                        "Sorry, CrossCam did an error. Please send me an error report so I can fix it! (Go to the Settings page to stop these popups.)",
                        "Send", "Don't Send");
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
                        await Device.InvokeOnMainThreadAsync(async () =>
                        {
                            await CoreMethods.DisplayAlert("Pair Role Not Selected",
                                "Please go to the Pairing page (via the Settings page) and choose a pairing role for this device before attempting to pair.",
                                "Ok");
                        });
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

        private void EvaluateOrientation(DisplayRotation rotation)
        {
            switch (rotation)
            {
                case DisplayRotation.Rotation0:
                    IsViewInverted = false;
                    IsViewPortrait = true;
                    break;
                case DisplayRotation.Rotation90:
                    IsViewInverted = false;
                    IsViewPortrait = false;
                    break;
                case DisplayRotation.Rotation180:
                    IsViewInverted = true;
                    IsViewPortrait = true;
                    break;
                case DisplayRotation.Rotation270:
                    IsViewInverted = true;
                    IsViewPortrait = false;
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
                    break;
            }
        }

        private void AlignmentSettingsOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            _isAlignmentInvalid = true;
        }

        private void BluetoothOperatorCountdownTimerSyncCompleteSecondary(object sender, EventArgs e)
        {
            if (!BluetoothOperator.IsPrimary)
            {
                IsHoldSteadySecondary = true;
            }
        }

        private void BluetoothOperatorTransmissionComplete(object sender, EventArgs e)
        {
            WorkflowStage = WorkflowStage.Capture;
        }

        private void BluetoothOperatorTransmissionStarted(object sender, EventArgs e)
        {
            IsHoldSteadySecondary = false;
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
            RaisePropertyChanged(nameof(CaptureButtonPosition));
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
            RaisePropertyChanged(nameof(CaptureButtonPosition));
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
                            SetLeftBitmap(SKBitmap.Decode(image1), SKEncodedOrigin.Default, false, true, true);
                            return;
                        }

                        if (RightBitmap == null &&
                            LeftBitmap != null)
                        {
                            SetRightBitmap(SKBitmap.Decode(image1), SKEncodedOrigin.Default, false, true, true);
                            return;
                        }

                        if (RightBitmap == null &&
                            LeftBitmap == null)
                        {
                            LocalCapturedFrame = new IncomingFrame
                            {
                                Frame = SKBitmap.Decode(image1),
                                Orientation = SKEncodedOrigin.Default
                            };
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
                    SetLeftBitmap(SKBitmap.Decode(image1), SKEncodedOrigin.Default, false, true, true);
                    SetRightBitmap(SKBitmap.Decode(image2), SKEncodedOrigin.Default, false, true, true);
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
            EvaluateOrientation(DeviceDisplay.MainDisplayInfo.Rotation);

            if (_isInitialized)
            {
                RaisePropertyChanged(nameof(ShouldLineGuidesBeVisible)); //TODO: figure out how to have Fody do this (just firing 'null' has bad behavior)
                RaisePropertyChanged(nameof(ShouldDonutGuideBeVisible));
                RaisePropertyChanged(nameof(ShouldRollGuideBeVisible));
                RaisePropertyChanged(nameof(ShouldSaveCapturesButtonBeVisible));
                RaisePropertyChanged(nameof(ShouldLeftLeftRetakeBeVisible));
                RaisePropertyChanged(nameof(ShouldLeftRightRetakeBeVisible));
                RaisePropertyChanged(nameof(ShouldRightLeftRetakeBeVisible));
                RaisePropertyChanged(nameof(ShouldRightRightRetakeBeVisible));
                RaisePropertyChanged(nameof(CaptureButtonPosition));
                RaisePropertyChanged(nameof(ShouldCenterLoadBeVisible));
                RaisePropertyChanged(nameof(ShouldLeftLoadBeVisible));
                RaisePropertyChanged(nameof(ShouldRightLoadBeVisible));
                RaisePropertyChanged(nameof(SavedSuccessMessage));
                RaisePropertyChanged(nameof(CanvasRectangle));
                RaisePropertyChanged(nameof(CanvasRectangleFlags));
                RaisePropertyChanged(nameof(PairButtonPosition));
                RaisePropertyChanged(nameof(ShouldPairPreviewBeVisible));
                RaisePropertyChanged(nameof(CameraViewModel));
                RaisePropertyChanged(nameof(Settings)); // this doesn't cause reevaluation for above stuff (but I'd like it to), but it does trigger redraw of canvas and evaluation of whether to run auto alignment
                RaisePropertyChanged(nameof(Settings.Mode));
                Settings.RaisePropertyChanged();
            }
            _isInitialized = true;

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
            if (!Settings.IsFovCorrectionSet &&
                Settings.IsPairedPrimary.HasValue &&
                Settings.IsPairedPrimary.Value &&
                BluetoothOperator.PairStatus == PairStatus.Connected)
            {
                await Device.InvokeOnMainThreadAsync(async () =>
                {
                    await CoreMethods.DisplayAlert("Field of View Correction",
                        "Different device models can have different fields of view. CrossCam will help you correct for this after you do your first capture. Frame up and capture something with distinctive points near the top and bottom of the frame, making sure the points are visible on both devices.",
                        "OK");
                });
            }
        }

        private void BluetoothOperatorOnErrorOccurred(object sender, ErrorEventArgs e)
        {
            _secondaryErrorOccurred = true;
        }

        private void BluetoothOperatorOnCapturedImageReceived(object sender, byte[] e)
        {
            RemotePreviewFrame = null;
            var bitmap = SKBitmap.Decode(e);
            if (Settings.IsCaptureLeftFirst)
            {
                SetRightBitmap(bitmap, SKEncodedOrigin.Default, false, false, true); //TODO: correct orientation?
            }
            else
            {
                SetLeftBitmap(bitmap, SKEncodedOrigin.Default, false, false, true); //TODO: correct orientation?
            }

            BluetoothOperator.SendTransmissionComplete();
        }

        private void TriggerMovementHint()
        {
            if ((LeftBitmap == null && RightBitmap != null) ||
                (LeftBitmap != null && RightBitmap == null) ||
                (BluetoothOperator.PairStatus == PairStatus.Connected &&
                RightBitmap == null &&
                LeftBitmap == null))
            {
                if (Settings.Mode == DrawMode.Cardboard)
                {
                    DoubleMoveHintTrigger = !DoubleMoveHintTrigger;
                }
                else
                {
                    SingleMoveHintTrigger = !SingleMoveHintTrigger;
                }
            }
        }

        private async Task DrawAnaglyph(bool grayscale)
        {
            var canvasWidth = DrawTool.CalculateOverlayedCanvasWidthWithEditsNoBorder(LeftBitmap, LeftOrientation,
                RightBitmap, RightOrientation, Edits);
            var canvasHeight = DrawTool.CalculateCanvasHeightWithEditsNoBorder(LeftBitmap, LeftOrientation, RightBitmap,
                RightOrientation, Edits);
            using var tempSurface =
                SKSurface.Create(new SKImageInfo(canvasWidth, canvasHeight));
            var canvas = tempSurface.Canvas;
            canvas.Clear(SKColor.Empty);

            DrawTool.DrawImagesOnCanvas(
                tempSurface, 
                LeftBitmap, LeftAlignmentTransform, LeftOrientation, IsLeftFrontFacing,
                RightBitmap, RightAlignmentTransform, RightOrientation, IsRightFrontFacing,
                Settings, Edits, grayscale ? DrawMode.GrayscaleRedCyanAnaglyph : DrawMode.RedCyanAnaglyph);

            await SaveSurfaceSnapshot(tempSurface, grayscale ? "GrayscaleAnaglyph" : "Anaglyph");
        }

        protected override void ViewIsDisappearing(object sender, EventArgs e)
        {
            BluetoothOperator.ErrorOccurred -= BluetoothOperatorOnErrorOccurred;

            base.ViewIsDisappearing(sender, e);
        }

        private void BluetoothOperatorOnPreviewFrameReceived(object sender, byte[] bytes)
        {
            RemotePreviewFrame = bytes;
        }

        private async Task SaveSurfaceSnapshot(SKSurface surface, string saveMode)
        {
            using var skImage = surface.Snapshot();
            using var encoded = skImage.Encode(SKEncodedImageFormat.Jpeg, 100);
            await _photoSaver.SavePhoto(encoded.ToArray(), Settings.SavingDirectory, Settings.SaveToExternal, saveMode);
        }

        private async Task<string> OpenLoadingPopup()
        {
            return await Device.InvokeOnMainThreadAsync(async () => await CoreMethods.DisplayActionSheet("Choose an action:", CANCEL, null,
                FULL_IMAGE, SINGLE_SIDE));
        }

        private async Task LoadFullStereoImage(byte[] image)
        {
            try
            {
                var leftHalf = await Task.Run(() => GetHalfOfFullStereoImage(image, true, Settings.ClipBorderOnNextLoad));
                SetLeftBitmap(leftHalf, SKEncodedOrigin.Default, false, false, true);
                var rightHalf = await Task.Run(() => GetHalfOfFullStereoImage(image, false, Settings.ClipBorderOnNextLoad));
                SetRightBitmap(rightHalf, SKEncodedOrigin.Default, false, false, true);
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

        private async void AutoAlign()
        {
            if (Settings.AlignmentSettings.IsAutomaticAlignmentOn &&
                LeftBitmap != null &&
                RightBitmap != null &&
                _isAlignmentInvalid &&
                0 == Interlocked.Exchange(ref _alignmentThreadLock, 1))
            {
                _isAlignmentInvalid = false;
                ClearEdits(false);
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
                            if ((WasCapturePaired &&
                                 (Settings.FovPrimaryCorrection > 0 ||
                                  Settings.FovSecondaryCorrection > 0)) ||
                                Settings.AlignmentSettings.UseKeypoints1)
                            {
                                var needsFallback = false;
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
                                        Settings.AlignmentSettings);
                                }
                            }
                            else
                            {
                                alignedResult = openCv.CreateAlignedSecondImageEcc(
                                    firstImage,
                                    secondImage,
                                    Settings.AlignmentSettings);
                            }
                        });
                    }
                    catch (Exception e)
                    {
                        ErrorMessage = e.ToString();
                    }

                    if (alignedResult != null)
                    {
                        if (Settings.AlignmentSettings.UseKeypoints1 &&
                            Settings.AlignmentSettings.DrawKeypointMatches &&
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
                            if (Device.RuntimePlatform == Device.iOS && IsViewInverted)
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

                            await SaveSurfaceSnapshot(dirtyMatchesSurface, "KeyPoints");
                            

                            if ((Settings.AlignmentSettings.DiscardOutliersBySlope || Settings.AlignmentSettings.DiscardOutliersByDistance) &&
                                alignedResult.DrawnCleanMatches != null)
                            {
                                using var cleanMatchesSurface =
                                    SKSurface.Create(new SKImageInfo(alignedResult.DrawnCleanMatches.Width,
                                        alignedResult.DrawnCleanMatches.Height));
                                var cleanMatchesCanvas = cleanMatchesSurface.Canvas;
                                cleanMatchesCanvas.Clear();
                                if (Device.RuntimePlatform == Device.iOS && IsViewInverted)
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

                                await SaveSurfaceSnapshot(cleanMatchesSurface, "KeyPoints");
                            }
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

        public void SetLeftBitmap(SKBitmap bitmap, SKEncodedOrigin orientation, bool isFrontFacing,
            bool withMovementTrigger, bool stepForward)
        {
            if (bitmap == null) return;

            IsLeftFrontFacing = isFrontFacing;
            LeftOrientation = orientation;
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
                    if (WasCapturePaired &&
                        BluetoothOperator.IsPrimary)
                    {
                        if (Settings.IsFovCorrectionSet)
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
                }
            }
        }

        public void SetRightBitmap(SKBitmap bitmap, SKEncodedOrigin orientation, bool isFrontFacing,
            bool withMovementTrigger, bool stepForward)
        {
            if (bitmap == null) return;

            IsRightFrontFacing = isFrontFacing;
            RightOrientation = orientation;
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
                    if (WasCapturePaired &&
                        BluetoothOperator.IsPrimary)
                    {
                        if (Settings.IsFovCorrectionSet)
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
                }
            }
        }

        private void CheckAndCorrectResolutionFovAndAspectDifferences()
        {
            if (!_isFovCorrected)
            {
                _isFovCorrected = true;
                float leftRatio;
                float rightRatio;
                if (LeftBitmap.Width < LeftBitmap.Height) //portrait
                {
                    leftRatio = LeftBitmap.Height / (1f * LeftBitmap.Width);
                    rightRatio = RightBitmap.Height / (1f * RightBitmap.Width);
                }
                else //landscape
                {
                    leftRatio = LeftBitmap.Width / (1f * LeftBitmap.Height);
                    rightRatio = RightBitmap.Width / (1f * RightBitmap.Height);
                }
                if (leftRatio != rightRatio)
                {
                    if (LeftBitmap.Height > LeftBitmap.Width) // portrait
                    {
                        if (leftRatio < rightRatio) // right is taller
                        {
                            var newWidth = (int)(LeftBitmap.Height / rightRatio);
                            var corrected = new SKBitmap(newWidth, LeftBitmap.Height);
                            using var canvas = new SKCanvas(corrected);
                            canvas.DrawBitmap(
                                LeftBitmap,
                                new SKRect((LeftBitmap.Width - newWidth) / 2f, 0, LeftBitmap.Width - (LeftBitmap.Width - newWidth) / 2f, LeftBitmap.Height),
                                new SKRect(0, 0, newWidth, LeftBitmap.Height));
                            
                            LeftBitmap = corrected;
                        }
                        else
                        {
                            var newWidth = (int)(RightBitmap.Height / leftRatio);
                            var corrected = new SKBitmap(newWidth, RightBitmap.Height);
                            using var canvas = new SKCanvas(corrected);
                            canvas.DrawBitmap(
                                RightBitmap,
                                new SKRect((RightBitmap.Width - newWidth) / 2f, 0, RightBitmap.Width - (RightBitmap.Width - newWidth) / 2f, RightBitmap.Height),
                                new SKRect(0, 0, newWidth, RightBitmap.Height));
                            
                            RightBitmap = corrected;
                        }
                    }
                    else //landscape
                    {
                        if (leftRatio > rightRatio) // left is wider
                        {
                            var newHeight = (int)(RightBitmap.Width * leftRatio);
                            var corrected = new SKBitmap(RightBitmap.Width, newHeight);
                            using var canvas = new SKCanvas(corrected);
                            canvas.DrawBitmap(
                                RightBitmap,
                                new SKRect(0, (RightBitmap.Height - newHeight) / 2f, RightBitmap.Width, RightBitmap.Height - (RightBitmap.Height - newHeight) / 2f),
                                new SKRect(0, 0, RightBitmap.Width, newHeight));
                            
                            RightBitmap = corrected;
                        }
                        else
                        {
                            var newHeight = (int)(LeftBitmap.Width * rightRatio);
                            var corrected = new SKBitmap(LeftBitmap.Width, newHeight);
                            using var canvas = new SKCanvas(corrected);
                            canvas.DrawBitmap(
                                LeftBitmap,
                                new SKRect(0, (LeftBitmap.Height - newHeight) / 2f, LeftBitmap.Width, LeftBitmap.Height - (LeftBitmap.Height - newHeight) / 2f),
                                new SKRect(0, 0, LeftBitmap.Width, newHeight));
                            
                            LeftBitmap = corrected;
                        }
                    }
                }

                if (Settings.IsFovCorrectionSet &&
                    (Settings.FovPrimaryCorrection != 0 ||
                     Settings.FovSecondaryCorrection != 0))
                {
                    double zoomAmount;
                    if (Settings.FovPrimaryCorrection > 0)
                    {
                        zoomAmount = Settings.FovPrimaryCorrection + 1;
                        if (Settings.IsCaptureLeftFirst)
                        {
                            LeftBitmap = ZoomBitmap(zoomAmount, LeftBitmap);
                        }
                        else
                        {
                            RightBitmap = ZoomBitmap(zoomAmount, RightBitmap);
                        }
                    }
                    else
                    {
                        zoomAmount = Settings.FovSecondaryCorrection + 1;
                        if (Settings.IsCaptureLeftFirst)
                        {
                            RightBitmap = ZoomBitmap(zoomAmount, RightBitmap);
                        }
                        else
                        {
                            LeftBitmap = ZoomBitmap(zoomAmount, LeftBitmap);
                        }
                    }
                }


                if (LeftBitmap.Width < RightBitmap.Width)
                {
                    var corrected = new SKBitmap(LeftBitmap.Width, LeftBitmap.Height);
                    using var canvas = new SKCanvas(corrected);
                    canvas.DrawBitmap(
                        RightBitmap,
                        new SKRect(0, 0, LeftBitmap.Width, LeftBitmap.Height));
                    
                    RightBitmap = corrected;
                }
                else if (RightBitmap.Width < LeftBitmap.Width)
                {
                    var corrected = new SKBitmap(RightBitmap.Width, RightBitmap.Height);
                    using var surface = new SKCanvas(corrected);
                    surface.DrawBitmap(
                        LeftBitmap,
                        new SKRect(0, 0, RightBitmap.Width, RightBitmap.Height));
                    
                    LeftBitmap = corrected;
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
            await Device.InvokeOnMainThreadAsync(async () =>
            {
                await CoreMethods.DisplayAlert("Field of View Correction", "To correct for field of view differences, zoom and slide the pictures so the distinctive points line up between the two photos. You can drag the white lines around to help you visualize the alignment. This correction will be applied to future previews. It will be saved but you can reset it on the Settings page. If you're using identical devices just save without adjusting.", "OK");
            });
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

            using var surface = new SKCanvas(extracted);
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

        private static int GetTotalColor(SKColor color)
        {
            return color.Red + color.Green + color.Blue;
        }

        private async Task EvaluateAndShowWelcomePopup()
        {
#if DEBUG
#else
            await Device.InvokeOnMainThreadAsync(async () =>
            {
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
            });
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
                _isAlignmentInvalid = true;
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
            Edits.Keystone = 0;
        }

        private void ClearEdits(bool andAutoAligmentFlags)
        {
            LeftAlignmentTransform = SKMatrix.Identity;
            RightAlignmentTransform = SKMatrix.Identity;

            ClearCrops(andAutoAligmentFlags);
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

            CameraColumn = Settings.IsCaptureLeftFirst ? 0 : 1;

            LeftBitmap = null;
            LeftAlignmentTransform = SKMatrix.Identity;
            LeftOrientation = SKEncodedOrigin.Default;
            IsLeftFrontFacing = false;

            RightBitmap = null;
            RightAlignmentTransform = SKMatrix.Identity;
            RightOrientation = SKEncodedOrigin.Default;
            IsRightFrontFacing = false;

            ClearEdits(true);
            _secondaryErrorOccurred = false;
            WorkflowStage = WorkflowStage.Capture;
            WasCapturePaired = false;
            _isFovCorrected = false;
            if (Settings.IsTapToFocusEnabled2)
            {
                SwitchToContinuousFocusTrigger = !SwitchToContinuousFocusTrigger;
            }
        }
    }
}