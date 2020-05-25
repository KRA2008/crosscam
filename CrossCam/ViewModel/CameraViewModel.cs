﻿using System;
using System.IO;
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

        public Command PairCommand { get; set; }

        public double ZoomMax => 1 / 4d;
        public double LeftZoom { get; set; }
        public double RightZoom { get; set; }

        public double LeftCrop { get; set; }
        public double RightCrop { get; set; }
        public double InsideCrop { get; set; }
        public double OutsideCrop { get; set; }
        public double TopCrop { get; set; }
        public double BottomCrop { get; set; }

        public double SideCropMax => 1 / 2d;
        public double TopOrBottomCropMax => 1 / 2d;

        public Command SetCropMode { get; set; }

        public double VerticalAlignmentMax => 1 / 8d;
        public double VerticalAlignment { get; set; }
        public double VerticalAlignmentMin => -VerticalAlignmentMax;

        public float RotationMax => 5;
        public float LeftRotation { get; set; }
        public float RightRotation { get; set; }
        public float RotationMin => -RotationMax;

        public Command SetKeystoneMode { get; set; }

        public float MaxKeystone => 1 / 4f;
        public float LeftKeystone { get; set; }
        public float RightKeystone { get; set; }

        public Command LoadPhotoCommand { get; set; }

        public bool IsViewPortrait { get; set; }
        public bool IsViewInverted { get; set; }
        public bool IsCaptureLeftFirst { get; set; }
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

        public double LeftFov => BluetoothOperator.IsPrimary && BluetoothOperator.PairStatus == PairStatus.Connected ? (IsCaptureLeftFirst ? BluetoothOperator.Fov : BluetoothOperator.PartnerFov) : 0; //TODO: it shouldn't go to 0 because you disconnect - otherwise you MUST stay connected while saving, which is bad
        public double RightFov => BluetoothOperator.IsPrimary && BluetoothOperator.PairStatus == PairStatus.Connected ? (IsCaptureLeftFirst ? BluetoothOperator.PartnerFov : BluetoothOperator.Fov) : 0;

        public bool IsNothingCaptured => LeftBitmap == null && RightBitmap == null;
        public bool ShouldIconBeVisible => IsNothingCaptured && IconColumn != CameraColumn && WorkflowStage == WorkflowStage.Capture;
        public bool ShouldLeftPairBeVisible => IsNothingCaptured && Settings.Handedness == Handedness.Right;
        public bool ShouldRightPairBeVisible => IsNothingCaptured && Settings.Handedness == Handedness.Left;
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
        public bool ShouldRightCaptureBeVisible => WorkflowStage == WorkflowStage.Capture && Settings.Handedness == Handedness.Right && (BluetoothOperatorBindable.PairStatus == PairStatus.Disconnected || Settings.IsPairedPrimary.HasValue && Settings.IsPairedPrimary.Value);
        public bool ShouldCenterCaptureBeVisible => WorkflowStage == WorkflowStage.Capture && Settings.Handedness == Handedness.Center && (BluetoothOperatorBindable.PairStatus == PairStatus.Disconnected || Settings.IsPairedPrimary.HasValue && Settings.IsPairedPrimary.Value);
        public bool ShouldLeftCaptureBeVisible => WorkflowStage == WorkflowStage.Capture && Settings.Handedness == Handedness.Left && (BluetoothOperatorBindable.PairStatus == PairStatus.Disconnected || Settings.IsPairedPrimary.HasValue && Settings.IsPairedPrimary.Value);

        public bool ShouldLineGuidesBeVisible => ((IsNothingCaptured && Settings.ShowGuideLinesWithFirstCapture)
                                                  || (IsExactlyOnePictureTaken && WorkflowStage != WorkflowStage.Loading)
                                                  || (BluetoothOperator.IsPrimary && BluetoothOperator.PairStatus == PairStatus.Connected && WorkflowStage == WorkflowStage.Capture))
                                                  && Settings.AreGuideLinesVisible
                                                  || WorkflowStage == WorkflowStage.Keystone
                                                  || WorkflowStage == WorkflowStage.ManualAlign;
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
                                               DrawTool.CalculateJoinedCanvasWidthLessBorder(LeftBitmap, RightBitmap,
                                                   LeftCrop + OutsideCrop, InsideCrop + RightCrop, InsideCrop + LeftCrop,
                                                   RightCrop + OutsideCrop) >
                                               DrawTool.CalculateCanvasHeightLessBorder(LeftBitmap, RightBitmap,
                                                   TopCrop, BottomCrop,
                                                   VerticalAlignment);

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
            BluetoothOperator = new BluetoothOperator(Settings);
            BluetoothOperator.Connected += BluetoothOperatorOnConnected;
            BluetoothOperator.Disconnected += BluetoothOperatorOnDisconnected;
            BluetoothOperator.PreviewFrameReceived += BluetoothOperatorOnPreviewFrameReceived;
            BluetoothOperator.CapturedImageReceived += BluetoothOperatorOnCapturedImageReceived;

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
                        if (IsCaptureLeftFirst)
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
                WorkflowStage = WorkflowStage == WorkflowStage.Edits ? WorkflowStage.Final : WorkflowStage.Edits;
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

            SwapSidesCommand = new Command(obj =>
            {
                var forced = obj as bool?;
                if (WorkflowStage == WorkflowStage.Capture ||
                    WorkflowStage == WorkflowStage.Final ||
                    WorkflowStage == WorkflowStage.Edits ||
                    forced.HasValue && forced.Value)
                {
                    IsCaptureLeftFirst = !IsCaptureLeftFirst;

                    var tempArray = LeftBitmap;
                    LeftBitmap = RightBitmap;
                    RightBitmap = tempArray;

                    if (WorkflowStage == WorkflowStage.Capture)
                    {
                        CameraColumn = CameraColumn == 0 ? 1 : 0;
                    }

                    TriggerMovementHint();

                    var tempCrop = InsideCrop;
                    InsideCrop = OutsideCrop;
                    OutsideCrop = tempCrop;

                    var tempRotate = LeftRotation;
                    LeftRotation = RightRotation;
                    RightRotation = tempRotate;

                    var tempKeystone = LeftKeystone;
                    LeftKeystone = RightKeystone;
                    RightKeystone = tempKeystone;

                    var tempZoom = LeftZoom;
                    LeftZoom = RightZoom;
                    RightZoom = tempZoom;

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

                                DrawTool.DrawImagesOnCanvas(canvas, LeftBitmap, RightBitmap,
                                    Settings.BorderWidthProportion, Settings.AddBorder, Settings.BorderColor,
                                    LeftCrop + OutsideCrop, InsideCrop + RightCrop, InsideCrop + LeftCrop,
                                    RightCrop + OutsideCrop,
                                    TopCrop, BottomCrop,
                                    LeftRotation, RightRotation,
                                    VerticalAlignment,
                                    LeftZoom, RightZoom,
                                    LeftKeystone, RightKeystone,
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

                                DrawTool.DrawImagesOnCanvas(canvas, RightBitmap, LeftBitmap,
                                    Settings.BorderWidthProportion, Settings.AddBorder, Settings.BorderColor,
                                    InsideCrop + LeftCrop, RightCrop + OutsideCrop,
                                    LeftCrop + OutsideCrop, InsideCrop + RightCrop, 
                                    TopCrop, BottomCrop,
                                    RightRotation, LeftRotation,
                                    VerticalAlignment,
                                    RightZoom, LeftZoom,
                                    RightKeystone, LeftKeystone,
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

                                canvas.DrawBitmap(IsCaptureLeftFirst ? LeftBitmap : RightBitmap, 0, 0);

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

        private void BluetoothOperatorOnDisconnected(object sender, EventArgs e)
        {
            RaisePropertyChanged(nameof(ShouldLeftCaptureBeVisible));
            RaisePropertyChanged(nameof(ShouldCenterCaptureBeVisible));
            RaisePropertyChanged(nameof(ShouldRightCaptureBeVisible));
            RaisePropertyChanged(nameof(ShouldLineGuidesBeVisible));
            RaisePropertyChanged(nameof(ShouldPairPreviewBeVisible));
            RaisePropertyChanged(nameof(ShouldDonutGuideBeVisible));
        }

        private void BluetoothOperatorOnConnected(object sender, EventArgs e)
        {
            RaisePropertyChanged(nameof(ShouldLeftCaptureBeVisible));
            RaisePropertyChanged(nameof(ShouldCenterCaptureBeVisible));
            RaisePropertyChanged(nameof(ShouldRightCaptureBeVisible));
            RaisePropertyChanged(nameof(ShouldLineGuidesBeVisible));
            RaisePropertyChanged(nameof(ShouldPairPreviewBeVisible));
            RaisePropertyChanged(nameof(ShouldDonutGuideBeVisible));
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

        private void BluetoothOperatorOnErrorOccurred(object sender, ErrorEventArgs e)
        {
            _secondaryErrorOccurred = true;
        }

        private void BluetoothOperatorOnCapturedImageReceived(object sender, byte[] e)
        {
            PreviewFrame = null;
            var bitmap = DecodeBitmapAndCorrectOrientation(e);
            if (IsCaptureLeftFirst)
            {
                SetRightBitmap(bitmap, false, true);
            }
            else
            {
                SetLeftBitmap(bitmap, false, true);
            }
        }

        private void TriggerMovementHint()
        {
            if (LeftBitmap != null &&
                RightBitmap == null)
            {
                if (Settings.Mode != DrawMode.Parallel)
                {
                    MoveLeftTrigger = !MoveLeftTrigger;
                }
                else
                {
                    MoveRightTrigger = !MoveRightTrigger;
                }
            }

            if (LeftBitmap == null &&
                RightBitmap != null)
            {

                if (Settings.Mode != DrawMode.Parallel)
                {
                    MoveRightTrigger = !MoveRightTrigger;
                }
                else
                {
                    MoveLeftTrigger = !MoveLeftTrigger;
                }
            }
        }

        private async Task DrawAnaglyph(bool grayscale)
        {
            var baseWidth = Math.Min(LeftBitmap.Width, RightBitmap.Width);
            var canvasWidth = (int)(baseWidth - baseWidth * (LeftCrop + InsideCrop + OutsideCrop + RightCrop));
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
                    LeftKeystone, RightKeystone,
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
                var leftHalf = await Task.Run(() => GetHalfOfFullStereoImage(image, true, Settings.ClipBorderOnLoad));
                SetLeftBitmap(leftHalf, false, true);
                var rightHalf = await Task.Run(() => GetHalfOfFullStereoImage(image, false, Settings.ClipBorderOnLoad));
                SetRightBitmap(rightHalf, false, true);
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
                    var firstImage = IsCaptureLeftFirst ? LeftBitmap : RightBitmap;
                    var secondImage = IsCaptureLeftFirst ? RightBitmap : LeftBitmap;
                    try
                    {
                        await Task.Run(() =>
                        {
                            alignedResult = openCv.CreateAlignedSecondImage(
                                firstImage,
                                secondImage,
                                Settings.AlignmentDownsizePercentage2,
                                Settings.AlignmentIterations2,
                                Settings.AlignmentEpsilonLevel2,
                                Settings.AlignmentEccThresholdPercentage2,
                                Settings.AlignmentPyramidLayers2,
                                !Settings.SaveForRedCyanAnaglyph && !Settings.AlignHorizontallySideBySide);
                        });
                    }
                    catch (Exception e)
                    {
                        ErrorMessage = e.ToString();
                    }

                    if (alignedResult != null)
                    {
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
                                TopCrop = topLeft.Y / secondImage.Height;
                            }
                        }
                        else
                        {
                            if (topRight.Y > 0)
                            {
                                TopCrop = topRight.Y / secondImage.Height;
                            }
                        }

                        var maxY = alignedResult.AlignedBitmap.Height - 1;
                        if (bottomLeft.Y < bottomRight.Y)
                        {
                            if (bottomLeft.Y < maxY)
                            {
                                BottomCrop = (maxY - bottomLeft.Y) / secondImage.Height;
                            }
                        }
                        else
                        {
                            if (bottomRight.Y < maxY)
                            {
                                BottomCrop = (maxY - bottomRight.Y) / secondImage.Height;
                            }
                        }

                        if (topLeft.X > bottomLeft.X)
                        {
                            if (topLeft.X > 0)
                            {
                                LeftCrop = topLeft.X / secondImage.Width;
                            }
                        }
                        else
                        {
                            if (bottomLeft.X > 0)
                            {
                                LeftCrop = bottomLeft.X / secondImage.Width;
                            }
                        }

                        var maxX = alignedResult.AlignedBitmap.Width - 1;
                        if (topRight.X < bottomRight.X)
                        {
                            if (topRight.X < maxX)
                            {
                                RightCrop = (maxX - topRight.X) / secondImage.Width;
                            }
                        }
                        else
                        {
                            if (bottomRight.X < maxX)
                            {
                                RightCrop = (maxX - bottomRight.X) / secondImage.Width;
                            }
                        }

                        if (IsCaptureLeftFirst)
                        {
                            _originalUnalignedBitmap = RightBitmap;
                            SetRightBitmap(alignedResult.AlignedBitmap, true, true);
                        }
                        else
                        {
                            _originalUnalignedBitmap = LeftBitmap;
                            SetLeftBitmap(alignedResult.AlignedBitmap, true, true);
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
                    if (BluetoothOperator.IsPrimary &&
                        BluetoothOperator.PairStatus == PairStatus.Connected)
                    {
                        CheckAndCorrectFovAndResolutionDifferences();
                    }
                    WasCaptureCross = Settings.Mode != DrawMode.Parallel;
                    CameraColumn = IsCaptureLeftFirst ? 0 : 1;
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
                    if (BluetoothOperator.IsPrimary &&
                        BluetoothOperator.PairStatus == PairStatus.Connected)
                    {
                        CheckAndCorrectFovAndResolutionDifferences();
                    }
                    WasCaptureCross = Settings.Mode != DrawMode.Parallel;
                    CameraColumn = IsCaptureLeftFirst ? 0 : 1;
                    WorkflowStage = WorkflowStage.Final;
                    AutoAlign();
                }
            }
        }

        private void CheckAndCorrectFovAndResolutionDifferences() //TODO: this seems like a fine approach but it doesn't look like it's working right...
        {
            var finalWidth = Math.Min(LeftBitmap.Width, RightBitmap.Width); // TODO: Android aspect ratio differences may make this ambiguous
            var finalHeight = Math.Min(LeftBitmap.Height, RightBitmap.Height);

            if (LeftFov > DrawTool.FLOATY_ZERO &&
                RightFov > DrawTool.FLOATY_ZERO)
            {
                if (LeftFov > RightFov)
                {
                    LeftBitmap = CorrectFovAndResolutionSide(LeftBitmap, RightFov / LeftFov, finalWidth, finalHeight);
                }
                else
                {
                    RightBitmap = CorrectFovAndResolutionSide(RightBitmap, LeftFov / RightFov, finalWidth, finalHeight);
                }
            }
        }

        private static SKBitmap CorrectFovAndResolutionSide(SKBitmap originalImage, double correctionProportion, int finalWidth, int finalHeight)
        {
            var leftFovCorrection = (originalImage.Width - originalImage.Width * correctionProportion) / 2d;
            var topFovCorrection = (originalImage.Height - originalImage.Height * correctionProportion) / 2d;

            var corrected = new SKBitmap(finalWidth, finalHeight);

            using (var canvas = new SKCanvas(corrected))
            {
                canvas.DrawBitmap(
                    originalImage,
                    SKRect.Create(
                        (float)leftFovCorrection,
                        (float)topFovCorrection,
                        (float)(originalImage.Width - leftFovCorrection),
                        (float)(originalImage.Height - topFovCorrection)),
                    SKRect.Create(
                        0,
                        0,
                        finalWidth,
                        finalHeight));
            }

            return corrected;
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

        public SKBitmap DecodeBitmapAndCorrectOrientation(byte[] bytes)
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
                    case SKCodecOrigin.BottomRight when !Settings.IsFrontCamera:
                        return BitmapRotate180(SKBitmap.Decode(bytes), false);
                    case SKCodecOrigin.BottomRight when Settings.IsFrontCamera:
                        return BitmapHorizontalMirror(SKBitmap.Decode(bytes));
                    case SKCodecOrigin.RightTop:
                        return BitmapRotate90(SKBitmap.Decode(bytes), Settings.IsFrontCamera);
                    case SKCodecOrigin.LeftBottom:
                        return BitmapRotate270(SKBitmap.Decode(bytes), Settings.IsFrontCamera);
                    case SKCodecOrigin.TopLeft when Settings.IsFrontCamera:
                        return BitmapRotate180(SKBitmap.Decode(bytes), true);
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
                BluetoothOperator.RequestClockReading();
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