﻿using System.ComponentModel;
using System.Diagnostics;
using System.Timers;
using CrossCam.CustomElement;
using CrossCam.Model;
using CrossCam.ViewModel;
using CrossCam.Wrappers;
using SkiaSharp;
using Rect = Microsoft.Maui.Graphics.Rect;
using Timer = System.Timers.Timer;
using Microsoft.Maui.Layouts;
using SkiaSharp.Views.Maui;
using Image = Microsoft.Maui.Controls.Image;
using PointF = System.Drawing.PointF;

namespace CrossCam.Page
{
    // ReSharper disable once UnusedMember.Global
    public partial class CameraPage
    {
	    private CameraViewModel _viewModel;
        private IDeviceDisplayWrapper _deviceDisplayWrapper;

        private readonly Rect _upperLineBoundsLandscape = new Rect(0, 0.33, 1, 21);
	    private readonly Rect _lowerLineBoundsLandscape = new Rect(0, 0.67, 1, 21);
	    private readonly Rect _upperLineBoundsPortrait = new Rect(0, 0.4, 1, 21);
	    private readonly Rect _lowerLinesBoundsPortrait = new Rect(0, 0.6, 1, 21);
        
	    private const float LEVEL_ICON_WIDTH = 60;
        private const float BUBBLE_LEVEL_MAX_TIP = 0.1f;
        private const float ROLL_GUIDE_MEASURMENT_WEIGHT = 12;
        private const float ROLL_GOOD_THRESHOLD = 0.01f;
        private readonly ImageSource _levelBubbleImage = ImageSource.FromFile("horizontallevelinside.png");
	    private readonly ImageSource _levelOutsideImage = ImageSource.FromFile("horizontalleveloutside.png");
	    private readonly ImageSource _levelBubbleGreenImage = ImageSource.FromFile("horizontallevelinsidegreen.png");
	    private readonly ImageSource _levelOutsideGreenImage = ImageSource.FromFile("horizontalleveloutsidegreen.png");

        private const float RETICLE_PANNER_WIDTH = 30;
        private const float RETICLE_IMAGE_WIDTH = 10;

        public const float FOCUS_CIRCLE_WIDTH = 30;

	    private float _reticleLeftX;
	    private float _reticleRightX;
	    private float _reticleY;

	    private float _upperLineY;
	    private float _upperLineHeight;

	    private float _lowerLineY;
	    private float _lowerLineHeight;

        private float _averageRoll;

        private Stopwatch _gyroscopeStopwatch;
        private float _cardboardViewVert;
        private float _cardboardViewHor;

        private float _lastAccelerometerReadingX;
        private float _lastAccelerometerReadingY;
        
        private float? _cardboardHomeVert;
        private float? _cardboardHomeHor;

        private bool _newLeftCapture;
        private bool _newRightCapture;

        private const SensorSpeed SENSOR_SPEED = SensorSpeed.Fastest;
        private const int SENSOR_FRAME_DELAY = 10;

        private PointF _tapLocation;
        private int _dragCounter;
        private int _releaseCounter;
        private readonly Timer _doubleTapTimer = new Timer
        {
            Interval = 500,
            AutoReset = false
        };
        private const int MIN_MOVE_COUNTER = 4;
        private bool _didSwap;
        private bool _forceCanvasClear;
        private readonly double _screenDensity;

        public CameraPage()
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);

            _deviceDisplayWrapper = DependencyService.Get<IDeviceDisplayWrapper>();
            _screenDensity = _deviceDisplayWrapper.GetDisplayDensity();

		    _horizontalLevelBubble.Source = _levelBubbleImage;
		    _horizontalLevelOutside.Source = _levelOutsideImage;

            _baseLayout.PropertyChanged += BaseLayoutOnPropertyChanged;
            _leftReticleLayout.PropertyChanged += LeftReticleLayoutOnPropertyChanged;
            _rightReticleLayout.PropertyChanged += RightReticleLayoutOnPropertyChanged;

            _gyroscopeStopwatch = new Stopwatch();
            PropertyChanged += OnCameraPagePropertyChanged;
        }

        private void OnCameraPagePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Width):
                case nameof(Height):
                    PlaceRollGuide();
                    break;
            }
        }

        private void RightReticleLayoutOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_rightReticleLayout.Width) ||
                e.PropertyName == nameof(_rightReticleLayout.Height))
            {
                ResetDonutGuide(_rightReticleLayout, _rightReticle, _rightReticlePanner, false);
            }
        }

        private void LeftReticleLayoutOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_leftReticleLayout.Width) ||
                e.PropertyName == nameof(_leftReticleLayout.Height))
            {
                ResetDonutGuide(_leftReticleLayout, _leftReticle, _leftReticlePanner, true);
            }
        }

        private void BaseLayoutOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_baseLayout.Height) ||
                e.PropertyName == nameof(_baseLayout.Width))
            {
                ResetLineGuides();
                SetCameraModuleSize();
            }
        }

        protected override bool OnBackButtonPressed()
	    {
	        return _viewModel?.BackButtonPressed() ?? base.OnBackButtonPressed();
	    }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            Accelerometer.ReadingChanged += StoreAccelerometerReading;
            Gyroscope.ReadingChanged += StoreGyroscopeReading;
            _doubleTapTimer.Elapsed += TapExpired;
            MessagingCenter.Subscribe<App>(this, App.APP_PAUSING_EVENT, o => EvaluateSensors(false));
            MessagingCenter.Subscribe<App>(this, App.APP_UNPAUSING_EVENT, o => EvaluateSensors());
            SetMarginsForNotch();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            Accelerometer.ReadingChanged -= StoreAccelerometerReading;
            Gyroscope.ReadingChanged -= StoreGyroscopeReading;
            _doubleTapTimer.Elapsed -= TapExpired;
            MessagingCenter.Unsubscribe<App>(this, App.APP_PAUSING_EVENT);
            MessagingCenter.Unsubscribe<App>(this, App.APP_UNPAUSING_EVENT);
        }

        private void SetMarginsForNotch()
        {
            var notchHeightProvider = DependencyService.Get<INotchHeightProvider>();
            if (notchHeightProvider != null &&
                _viewModel != null)
            {
                var padding = Padding;
                padding.Top = _viewModel.IsViewPortrait ? notchHeightProvider.GetNotchHeight() : 0;
                padding.Bottom = _viewModel.IsViewPortrait ? notchHeightProvider.GetHomeThingHeight() : 0;
                Padding = padding;
            }
        }

        private void EvaluateSensors(bool isAppRunning = true)
	    {
	        if (_viewModel != null)
	        {
	            if (_viewModel.Settings.ShowRollGuide &&
                    _viewModel.WorkflowStage == WorkflowStage.Capture &&
                    isAppRunning)
	            {
	                if (!Accelerometer.IsMonitoring)
	                {
                        try
                        {
                            Accelerometer.Start(SENSOR_SPEED);
                            StartAccelerometerCycling();
                        }
                        catch (FeatureNotSupportedException e)
                        {
                            //now what?
                        }
                        catch (Exception e)
                        {
                            _viewModel.Error = e;
                        }
                    }
	            }
	            else
	            {
	                if (Accelerometer.IsMonitoring)
	                {
	                    try
	                    {
                            Accelerometer.Stop();
                        }
	                    catch
	                    {
                            //oh well
	                    }
                    }
	            }

                if (_viewModel.Settings.Mode == DrawMode.Cardboard &&
                    isAppRunning)
                {
                    if (!Gyroscope.IsMonitoring)
                    {
                        try
                        {
                            _gyroscopeStopwatch.Start();
                            Gyroscope.Start(SENSOR_SPEED);
                        }
                        catch (FeatureNotSupportedException e)
                        {
                            //now what?
                        }
                        catch (Exception e)
                        {
                            _viewModel.Error = e;
                        }
                    }
                }
                else
                {
                    if (Gyroscope.IsMonitoring)
                    {
                        try
                        {
                            Gyroscope.Stop();
                        }
                        catch
                        {
                            //oh well
                        }
                    }
                }
            }
        }

        private async void StartAccelerometerCycling()
        {
            while (Accelerometer.IsMonitoring)
            {
                await Task.Delay(SENSOR_FRAME_DELAY);
                UpdateLevelFromAccelerometerData();
            }
        }

        private void StoreGyroscopeReading(object sender, GyroscopeChangedEventArgs e)
        {
            if (!_viewModel.IsBusy)
            {
                var seconds = _gyroscopeStopwatch.ElapsedTicks / 10000000f;
                _cardboardViewVert -= e.Reading.AngularVelocity.Y * seconds;
                _cardboardViewHor += e.Reading.AngularVelocity.X * seconds;
                _gyroscopeStopwatch.Restart();
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _canvas.InvalidateSurface();
                });
            }
            else
            {
                _cardboardViewVert = _cardboardViewHor = 0;
                _cardboardHomeVert = _cardboardHomeHor = null;
            }
        }

        private void StoreAccelerometerReading(object sender, AccelerometerChangedEventArgs args)
        {
            _lastAccelerometerReadingX = args.Reading.Acceleration.X;
            _lastAccelerometerReadingY = args.Reading.Acceleration.Y;
        }

        private void UpdateLevelFromAccelerometerData()
        {
            _averageRoll *= (ROLL_GUIDE_MEASURMENT_WEIGHT - 1) / ROLL_GUIDE_MEASURMENT_WEIGHT;

            if (_viewModel != null)
            {
                var xReading = _lastAccelerometerReadingX; //thread protection
                var yReading = _lastAccelerometerReadingY;
                if (Math.Abs(xReading) < Math.Abs(yReading)) //portrait
                {
                    if (_viewModel.IsViewInverted)
                    {
                        _averageRoll -= xReading / ROLL_GUIDE_MEASURMENT_WEIGHT;
                    }
                    else
                    {
                        _averageRoll += xReading / ROLL_GUIDE_MEASURMENT_WEIGHT;
                    }
                }
                else //landscape
                {
                    if (_viewModel.IsViewInverted)
                    {
                        _averageRoll += yReading / ROLL_GUIDE_MEASURMENT_WEIGHT;
                    }
                    else
                    {
                        _averageRoll -= yReading / ROLL_GUIDE_MEASURMENT_WEIGHT;
                    }
                }

                ImageSource targetBubbleImage = null;
                ImageSource targetOutsideImage = null;

                if (Math.Abs(_averageRoll) < ROLL_GOOD_THRESHOLD &&
                    _horizontalLevelBubble.Source == _levelBubbleImage)
                {
                    targetBubbleImage = _levelBubbleGreenImage;
                    targetOutsideImage = _levelOutsideGreenImage;
                }
                else if (Math.Abs(_averageRoll) > ROLL_GOOD_THRESHOLD &&
                         _horizontalLevelBubble.Source == _levelBubbleGreenImage)
                {
                    targetBubbleImage = _levelBubbleImage;
                    targetOutsideImage = _levelOutsideImage;
                }

                var newX = 0.5 + _averageRoll / BUBBLE_LEVEL_MAX_TIP / 2;
                if (newX > 1)
                {
                    newX = 1;
                }
                else if (newX < 0)
                {
                    newX = 0;
                }

                var bubbleBounds = AbsoluteLayout.GetLayoutBounds(_horizontalLevelBubble);
                bubbleBounds.X = newX;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (targetBubbleImage != null)
                    {
                        _horizontalLevelBubble.Source = targetBubbleImage;
                    }
                    if (targetOutsideImage != null)
                    {
                        _horizontalLevelOutside.Source = targetOutsideImage;
                    }
                    AbsoluteLayout.SetLayoutBounds(_horizontalLevelBubble, bubbleBounds);
                });
            }
        }

        protected override void OnBindingContextChanged()
	    {
	        base.OnBindingContextChanged();
	        if (BindingContext != null)
	        {
	            _viewModel = (CameraViewModel) BindingContext;
	            _viewModel.PropertyChanged += ViewModelPropertyChanged;
                _viewModel.Settings.PropertyChanged += SettingsOnPropertyChanged;
                _viewModel.Edits.PropertyChanged += InvalidateCanvasBecausePropertyChanged;
                _viewModel.Settings.CardboardSettings.PropertyChanged += InvalidateCanvasBecausePropertyChanged;
                _viewModel.Settings.AlignmentSettings.PropertyChanged += InvalidateCanvasBecausePropertyChanged;
                _viewModel.PairOperatorBindable.PropertyChanged += PairOperatorBindableOnPropertyChanged;

                var layout = AbsoluteLayout.GetLayoutBounds(_moveHintSideStack);
                layout.Width = _viewModel.Settings.CardboardSettings.CardboardIpd + 100;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    AbsoluteLayout.SetLayoutBounds(_moveHintSideStack, layout);
                    _viewModel.DisplayRotation = DeviceDisplay.MainDisplayInfo.Rotation;
                    _viewModel.DisplayOrientation = DeviceDisplay.MainDisplayInfo.Orientation;
                    EvaluateSensors();
                    PlaceRollGuide();
                });
            }
	    }

        private void PairOperatorBindableOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PairOperator.PairStatus))
            {
                PlaceRollGuide();
            }
        }

        private void SettingsOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Settings.ShowRollGuide):
                case nameof(Settings.Mode):
                    EvaluateSensors();
                    break;
                case nameof(Settings.IsCaptureLeftFirst):
                    PlaceRollGuide();
                    break;
                case nameof(Settings.AddBorder2):
                case nameof(Settings.BorderWidthProportion):
                case nameof(Settings.BorderColor):
                case nameof(Settings.SaveWithFuseGuide):
                    _canvas.InvalidateSurface();
                    break;
            }
        }

        private void InvalidateCanvasBecausePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _canvas.InvalidateSurface();
            });
        }

        private void ViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
	    {
            switch (e.PropertyName)
            {
                case nameof(CameraViewModel.FocusCircleX):
                case nameof(CameraViewModel.FocusCircleY):
                    MoveFocusCircle();
                    break;
                case nameof(CameraViewModel.WorkflowStage):
                    //_cameraSettingsBox.ForceLayout(); //TODO: needed?
                    if (_viewModel.WorkflowStage != WorkflowStage.View)
                    {
                        _viewModel.Explore.Clear();
                    }
                    EvaluateSensors();
                    _forceCanvasClear = true;
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        _canvas.InvalidateSurface();
                    });
                    break;
                case nameof(CameraViewModel.CameraColumn):
                    PlaceRollGuide();
                    break;
                case nameof(CameraViewModel.IsViewPortrait):
                    Debug.WriteLine("### isViewPortrait changed");
                    ProcessDoubleTap();
                    SetMarginsForNotch();
                    _forceCanvasClear = true; 
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        _canvas.InvalidateSurface();
                    });
                    break;
                case nameof(CameraViewModel.LeftBitmap):
                case nameof(CameraViewModel.LeftAlignmentTransform):
                    ProcessDoubleTap();
                    _newLeftCapture = true;
                    CardboardCheckAndSaveOrientationSnapshot(); 
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        _canvas.InvalidateSurface();
                    });
                    break;
                case nameof(CameraViewModel.RightBitmap):
                case nameof(CameraViewModel.RightAlignmentTransform):
                    ProcessDoubleTap();
                    _newRightCapture = true;
                    CardboardCheckAndSaveOrientationSnapshot();
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        _canvas.InvalidateSurface();
                    });
                    break;
                case nameof(CameraViewModel.RemotePreviewFrame):
                case nameof(CameraViewModel.LocalPreviewFrame):
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        _canvas.InvalidateSurface();
                    });
                    break;
                case nameof(CameraViewModel.IsFullscreenToggle):
                case nameof(CameraViewModel.IsNothingCaptured):
                    PlaceRollGuide();
                    _forceCanvasClear = true;
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        _canvas.InvalidateSurface();
                    });
                    break;
                case nameof(CameraViewModel.IsBusy):
                    CardboardCheckAndSaveOrientationSnapshot();
                    break;
                case nameof(CameraViewModel.PreviewAspectRatio):
                    SetCameraModuleSize();
                    PlaceRollGuide();
                    break;
                case nameof(CameraViewModel.DisplayOrientation):
                case nameof(CameraViewModel.DisplayRotation):
                    PlaceRollGuide();

                    break;
            }
	    }

        private void SetCameraModuleSize()
        {
            var layoutBounds = AbsoluteLayout.GetLayoutBounds(_cameraModule);
            layoutBounds.Width = this.Width;
            layoutBounds.Height = this.Width * _viewModel.PreviewAspectRatio;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                AbsoluteLayout.SetLayoutBounds(_cameraModule, layoutBounds);
            });
        }

        private void CardboardCheckAndSaveOrientationSnapshot()
        {
            if (_viewModel.LeftBitmap == null ||
                _viewModel.RightBitmap == null)
            {
                _cardboardHomeVert = _cardboardHomeHor = null;
            } 
            else if (_viewModel?.LeftBitmap != null &&
                     _viewModel.RightBitmap != null &&
                     !_cardboardHomeVert.HasValue &&
                     !_cardboardHomeHor.HasValue &&
                     _viewModel.Settings.Mode == DrawMode.Cardboard)
            {
                _cardboardHomeVert = _cardboardViewVert;
                _cardboardHomeHor = _cardboardViewHor;
            }
        }

        private void MoveFocusCircle()
        {
            if (_viewModel != null)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    AbsoluteLayout.SetLayoutBounds(_focusCircle, new Rect
                    {
                        X = _viewModel.FocusCircleX - FOCUS_CIRCLE_WIDTH / 2d,
                        Y = _viewModel.FocusCircleY - FOCUS_CIRCLE_WIDTH / 2d,
                        Width = FOCUS_CIRCLE_WIDTH,
                        Height = FOCUS_CIRCLE_WIDTH
                    });
                });
            }
        }

        private void OnCanvasInvalidated(object sender, SKPaintSurfaceEventArgs e)
	    {
            //Debug.WriteLine("### left: " + _viewModel.LeftBitmap + " right: " + _viewModel.RightBitmap + " preview: " + _viewModel.LocalPreviewFrame + " captured: " + _viewModel.LocalCapturedFrame);
            var surface = e.Surface;

            var clearCanvas = _viewModel.Settings.Mode == DrawMode.RedCyanAnaglyph ||
                              _viewModel.Settings.Mode == DrawMode.GrayscaleRedCyanAnaglyph ||
                              _viewModel.Settings.FullscreenCapturing ||
                              _viewModel.Settings.FullscreenEditing ||
                              _forceCanvasClear;
            _forceCanvasClear = false;

            if (_viewModel.LeftBitmap == null &&
                _viewModel.RightBitmap == null)
            {
                surface.Canvas.Clear();
                _cardboardHomeHor = null;
                _cardboardHomeVert = null;
            }

            if (clearCanvas)
            {
                surface.Canvas.Clear();
            }
            
            SKBitmap left = null;
            SKBitmap right = null;
            var leftAlignment = SKMatrix.Identity;
            var rightAlignment = SKMatrix.Identity;
            SKEncodedOrigin? leftOrientation = SKEncodedOrigin.Default;
            SKEncodedOrigin? rightOrientation = SKEncodedOrigin.Default;
            var isLeftFrontFacing = false;
            var isRightFrontFacing = false;

            if (_viewModel.LeftBitmap != null &&
                _viewModel.RightBitmap != null)
            {
                surface.Canvas.Clear();

                left = _viewModel.LeftBitmap;
                leftAlignment = _viewModel.LeftAlignmentTransform;
                leftOrientation = SKEncodedOrigin.Default;
                isLeftFrontFacing = false;
                right = _viewModel.RightBitmap;
                rightAlignment = _viewModel.RightAlignmentTransform;
                rightOrientation = SKEncodedOrigin.Default;
                isRightFrontFacing = false;
            }
            else
            {
                if (_newLeftCapture || 
                    clearCanvas &&
                    _viewModel.LeftBitmap != null)
                {
                    left = _viewModel.LeftBitmap;
                    leftAlignment = _viewModel.LeftAlignmentTransform;
                    leftOrientation = SKEncodedOrigin.Default;
                    isLeftFrontFacing = false;
                    _newLeftCapture = false;
                }
                else if (_viewModel.CameraColumn == 0 &&
                         !_newRightCapture)
                {
                    left = _viewModel.LocalPreviewFrame?.Frame;
                    leftOrientation = _viewModel.LocalPreviewFrame?.Orientation;
                    isLeftFrontFacing = _viewModel.LocalPreviewFrame?.IsFrontFacing ?? false;

                    if (_viewModel.PairOperatorBindable.PairStatus == PairStatus.Connected)
                    {
                        right = _viewModel.RemotePreviewFrame?.Frame;
                        rightOrientation = _viewModel.RemotePreviewFrame?.Orientation;
                        isRightFrontFacing = _viewModel.LocalPreviewFrame?.IsFrontFacing ?? false;
                    }
                }

                if (_newRightCapture || 
                    clearCanvas &&
                    _viewModel.RightBitmap != null)
                {
                    right = _viewModel.RightBitmap;
                    rightAlignment = _viewModel.RightAlignmentTransform;
                    rightOrientation = SKEncodedOrigin.Default;
                    isRightFrontFacing = false;
                    _newRightCapture = false;
                }
                else if (_viewModel.CameraColumn == 1 &&
                         !_newLeftCapture)
                {
                    right = _viewModel.LocalPreviewFrame?.Frame;
                    rightOrientation = _viewModel.LocalPreviewFrame?.Orientation;
                    isRightFrontFacing = _viewModel.LocalPreviewFrame?.IsFrontFacing ?? false;

                    if (_viewModel.PairOperatorBindable.PairStatus == PairStatus.Connected)
                    {
                        left = _viewModel.RemotePreviewFrame?.Frame;
                        leftOrientation = _viewModel.RemotePreviewFrame?.Orientation;
                        isLeftFrontFacing = _viewModel.LocalPreviewFrame?.IsFrontFacing ?? false;
                    }
                }
            }

            if (_viewModel.Settings.IsCaptureInMirrorMode &&
                _viewModel.PairOperatorBindable.PairStatus != PairStatus.Connected &&
                (_viewModel.LeftBitmap == null ||
                _viewModel.RightBitmap == null))
            {
                left = right = _viewModel.LocalPreviewFrame?.Frame;
                leftOrientation = rightOrientation = _viewModel.LocalPreviewFrame?.Orientation;
                isLeftFrontFacing = isRightFrontFacing = _viewModel.LocalPreviewFrame?.IsFrontFacing ?? false;
            }

            if (_viewModel.Settings.Mode == DrawMode.Cardboard &&
                _viewModel.LeftBitmap == null &&
                _viewModel.RightBitmap == null)
            {
                if (_viewModel.PairOperatorBindable.PairStatus == PairStatus.Connected)
                {
                    if (_viewModel.Settings.IsCaptureLeftFirst)
                    {
                        left = _viewModel.LocalPreviewFrame?.Frame;
                        leftOrientation = _viewModel.LocalPreviewFrame?.Orientation;
                        isLeftFrontFacing = _viewModel.LocalPreviewFrame?.IsFrontFacing ?? false;
                        right = _viewModel.RemotePreviewFrame?.Frame;
                        rightOrientation = _viewModel.RemotePreviewFrame?.Orientation;
                        isRightFrontFacing = _viewModel.LocalPreviewFrame?.IsFrontFacing ?? false;
                    }
                    else
                    {
                        right = _viewModel.LocalPreviewFrame?.Frame;
                        rightOrientation = _viewModel.LocalPreviewFrame?.Orientation;
                        isRightFrontFacing = _viewModel.LocalPreviewFrame?.IsFrontFacing ?? false;
                        left = _viewModel.RemotePreviewFrame?.Frame;
                        leftOrientation = _viewModel.RemotePreviewFrame?.Orientation;
                        isLeftFrontFacing = _viewModel.LocalPreviewFrame?.IsFrontFacing ?? false;
                    }
                }
                else
                {
                    left = right = _viewModel.LocalPreviewFrame?.Frame;
                    leftOrientation = rightOrientation = _viewModel.LocalPreviewFrame?.Orientation;
                    isLeftFrontFacing = isRightFrontFacing = _viewModel.LocalPreviewFrame?.IsFrontFacing ?? false;
                }
            }

            float cardboardVert = 0;
            if (_cardboardHomeVert.HasValue)
            {
                if (_viewModel.IsViewInverted)
                {
                    cardboardVert = _cardboardViewVert - _cardboardHomeVert.Value;
                    if (cardboardVert > 0.5)
                    {
                        _cardboardHomeVert = _cardboardHomeVert.Value + (cardboardVert - 0.5f);
                        cardboardVert = 0.5f;
                    }
                    else if (cardboardVert < -0.5)
                    {
                        _cardboardHomeVert = _cardboardHomeVert.Value + (cardboardVert + 0.5f);
                        cardboardVert = -0.5f;
                    }
                }
                else
                {
                    cardboardVert = _cardboardHomeVert.Value - _cardboardViewVert;
                    if (cardboardVert > 0.5)
                    {
                        _cardboardHomeVert = _cardboardHomeVert.Value - (cardboardVert - 0.5f);
                        cardboardVert = 0.5f;
                    }
                    else if (cardboardVert < -0.5)
                    {
                        _cardboardHomeVert = _cardboardHomeVert.Value - (cardboardVert + 0.5f);
                        cardboardVert = -0.5f;
                    }
                }
            }

            float cardboardHor = 0;
            if (_cardboardHomeHor.HasValue)
            {
                if (_viewModel.IsViewInverted)
                {
                    cardboardHor = _cardboardViewHor - _cardboardHomeHor.Value;
                    if (cardboardHor > 0.5)
                    {
                        _cardboardHomeHor = _cardboardHomeHor.Value + (cardboardHor - 0.5f);
                        cardboardHor = 0.5f;
                    }
                    else if (cardboardHor < -0.5)
                    {
                        _cardboardHomeHor = _cardboardHomeHor.Value + (cardboardHor + 0.5f);
                        cardboardHor = -0.5f;
                    }
                }
                else
                {
                    cardboardHor = _cardboardHomeHor.Value - _cardboardViewHor;
                    if (cardboardHor > 0.5)
                    {
                        _cardboardHomeHor = _cardboardHomeHor.Value - (cardboardHor - 0.5f);
                        cardboardHor = 0.5f;
                    }
                    else if (cardboardHor < -0.5)
                    {
                        _cardboardHomeHor = _cardboardHomeHor.Value - (cardboardHor + 0.5f);
                        cardboardHor = -0.5f;
                    }
                }
            }

            var drawQuality = 
                _viewModel.IsExactlyOnePictureTaken || _viewModel.IsNothingCaptured
                ? DrawQuality.Preview
                : DrawQuality.Review;
            DrawTool.DrawImagesOnCanvas(
                surface,
                left, leftAlignment,
                right, rightAlignment,
                _viewModel.Settings,
                _viewModel.Edits,
                _viewModel.Settings.Mode,
                _viewModel.PairOperatorBindable.PairStatus == PairStatus.Connected || _viewModel.WasCapturePaired,
                isLeftFrontFacing,
                leftOrientation ?? SKEncodedOrigin.Default,
                isRightFrontFacing,
                rightOrientation ?? SKEncodedOrigin.Default,
                drawQuality: drawQuality,
                cardboardVert: cardboardVert,
                cardboardHor: cardboardHor,
                isFovStage: _viewModel.WorkflowStage == WorkflowStage.FovCorrection,
                useFullscreen:
                (drawQuality == DrawQuality.Preview &&
                 _viewModel.Settings.FullscreenCapturing ||
                 drawQuality == DrawQuality.Review &&
                 _viewModel.Settings.FullscreenEditing) &&
                (_viewModel.Settings.Mode == DrawMode.Cross ||
                 _viewModel.Settings.Mode == DrawMode.Parallel ||
                 _viewModel.Settings.Mode == DrawMode.Cardboard &&
                 _viewModel.WorkflowStage != WorkflowStage.Capture) ||
                _viewModel.IsNothingCaptured && 
                (_viewModel.PairOperatorBindable.PairStatus != PairStatus.Connected ||
                 _viewModel.PairOperatorBindable.PairStatus == PairStatus.Connected &&
                 _viewModel.Settings.PairSettings.IsPairedPrimary.HasValue &&
                 !_viewModel.Settings.PairSettings.IsPairedPrimary.Value) &&
                _viewModel.Settings.Mode != DrawMode.Cardboard &&
                !(_viewModel.Settings.IsCaptureInMirrorMode &&
                  !_viewModel.Settings.FullscreenCapturing),
                useMirrorCapture: _viewModel.Settings.IsCaptureInMirrorMode &&
                                  drawQuality == DrawQuality.Preview,
                explore:_viewModel.Explore);

            if (_viewModel.PairOperatorBindable.PairStatus == PairStatus.Connected &&
                _viewModel.WorkflowStage == WorkflowStage.Capture)
            {
                _viewModel.PairOperatorBindable.RequestPreviewFrame();
            }
        }

        private void ResetLineGuides()
        {
            Rect upperLineBounds, lowerLineBounds;
            if (_viewModel.DisplayOrientation == DisplayOrientation.Portrait)
            {
                upperLineBounds = _upperLineBoundsPortrait;
                lowerLineBounds = _lowerLinesBoundsPortrait;
            }
            else
            {
                upperLineBounds = _upperLineBoundsLandscape;
                lowerLineBounds = _lowerLineBoundsLandscape;
            }
            MainThread.BeginInvokeOnMainThread(() =>
            {            
                AbsoluteLayout.SetLayoutFlags(_upperLeftLine, AbsoluteLayoutFlags.PositionProportional | AbsoluteLayoutFlags.WidthProportional);
                AbsoluteLayout.SetLayoutBounds(_upperLeftLine, new Rect(0, upperLineBounds.Y, 0.5, upperLineBounds.Height));
                AbsoluteLayout.SetLayoutFlags(_upperRightLine, AbsoluteLayoutFlags.PositionProportional | AbsoluteLayoutFlags.WidthProportional);
                AbsoluteLayout.SetLayoutBounds(_upperRightLine, new Rect(1, upperLineBounds.Y, 0.5, upperLineBounds.Height));
                AbsoluteLayout.SetLayoutFlags(_upperLinePanner, AbsoluteLayoutFlags.YProportional | AbsoluteLayoutFlags.WidthProportional);
                AbsoluteLayout.SetLayoutBounds(_upperLinePanner, upperLineBounds);

                AbsoluteLayout.SetLayoutFlags(_lowerLeftLine, AbsoluteLayoutFlags.PositionProportional | AbsoluteLayoutFlags.WidthProportional);
                AbsoluteLayout.SetLayoutBounds(_lowerLeftLine, new Rect(0, lowerLineBounds.Y, 0.5, lowerLineBounds.Height));
                AbsoluteLayout.SetLayoutFlags(_lowerRightLine, AbsoluteLayoutFlags.PositionProportional | AbsoluteLayoutFlags.WidthProportional);
                AbsoluteLayout.SetLayoutBounds(_lowerRightLine, new Rect(1, lowerLineBounds.Y, 0.5, lowerLineBounds.Height));
                AbsoluteLayout.SetLayoutFlags(_lowerLinePanner, AbsoluteLayoutFlags.YProportional | AbsoluteLayoutFlags.WidthProportional);
                AbsoluteLayout.SetLayoutBounds(_lowerLinePanner, lowerLineBounds);
            });

        }

        private void ResetDonutGuide(AbsoluteLayout layout, Image reticle, ContentView reticlePanner, bool isLeft)
        {
            if (_viewModel?.Settings.IsCaptureInMirrorMode == true &&
                _viewModel.DisplayOrientation != DisplayOrientation.Portrait)
            {
                var previewHeight = Math.Min(layout.Height, layout.Width);
                var fullPreviewWidth = Math.Max(layout.Height, layout.Width);
                double sidePreviewWidth;
                if (_viewModel.Settings.ShowPreviewFuseGuide)
                {
                    var previewHeightWithFuseGuide = previewHeight - DrawTool.CalculateFuseGuideMarginHeight((float) previewHeight);
                    sidePreviewWidth = _viewModel.PreviewAspectRatio * previewHeightWithFuseGuide / 2d;
                }
                else
                {
                    sidePreviewWidth = _viewModel.PreviewAspectRatio * previewHeight / 2d;
                }

                if (isLeft)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        AbsoluteLayout.SetLayoutBounds(reticle,
                        new Rect(fullPreviewWidth - sidePreviewWidth / 2d - RETICLE_IMAGE_WIDTH / 2d,
                            previewHeight / 2d - RETICLE_IMAGE_WIDTH / 2d,
                            RETICLE_IMAGE_WIDTH, RETICLE_IMAGE_WIDTH));
                        AbsoluteLayout.SetLayoutBounds(reticlePanner,
                        new Rect(fullPreviewWidth - sidePreviewWidth / 2d - RETICLE_PANNER_WIDTH / 2d,
                            previewHeight / 2d - RETICLE_PANNER_WIDTH / 2d,
                            RETICLE_PANNER_WIDTH, RETICLE_PANNER_WIDTH));
                    });
                    
                }
                else
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        AbsoluteLayout.SetLayoutBounds(reticle,
                        new Rect(sidePreviewWidth / 2d - RETICLE_IMAGE_WIDTH / 2d,
                            previewHeight / 2d - RETICLE_IMAGE_WIDTH / 2d,
                            RETICLE_IMAGE_WIDTH, RETICLE_IMAGE_WIDTH));
                        AbsoluteLayout.SetLayoutBounds(reticlePanner,
                        new Rect(sidePreviewWidth / 2d - RETICLE_PANNER_WIDTH / 2d,
                            previewHeight / 2d - RETICLE_PANNER_WIDTH / 2d,
                            RETICLE_PANNER_WIDTH, RETICLE_PANNER_WIDTH));
                    });
                }
            }
            else
            {
                double sideWidth, sideHeight;
                if (_viewModel?.DisplayOrientation == DisplayOrientation.Portrait)
                {
                    sideWidth = Math.Min(layout.Width, layout.Height);
                    sideHeight = Math.Max(layout.Width, layout.Height);
                }
                else
                {
                    sideWidth = Math.Max(layout.Width, layout.Height);
                    sideHeight = Math.Min(layout.Width, layout.Height);
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    AbsoluteLayout.SetLayoutBounds(reticle,
                    new Rect(sideWidth / 2d - RETICLE_IMAGE_WIDTH / 2d,
                        sideHeight / 2d - RETICLE_IMAGE_WIDTH / 2d,
                        RETICLE_IMAGE_WIDTH, RETICLE_IMAGE_WIDTH));
                    AbsoluteLayout.SetLayoutBounds(reticlePanner,
                    new Rect(sideWidth / 2d - RETICLE_PANNER_WIDTH / 2d,
                        sideHeight / 2d - RETICLE_PANNER_WIDTH / 2d,
                        RETICLE_PANNER_WIDTH, RETICLE_PANNER_WIDTH));
                });
            }
        }

        private void PlaceRollGuide()
        {
            var rollBounds = AbsoluteLayout.GetLayoutBounds(_horizontalLevelWhole);
            rollBounds.Width = LEVEL_ICON_WIDTH;
            rollBounds.Height = LEVEL_ICON_WIDTH;
            if (_viewModel == null)
            {
                rollBounds.X = 0.2;
                rollBounds.Y = Height - LEVEL_ICON_WIDTH;
            }
            else
            {
                var apertureHeight = (double)Application.Current.Resources["_giantIconWidth"];
                var bottomPadding = (Thickness)Application.Current.Resources["_bottomPadding"];
                var apertureY = Height - (apertureHeight + bottomPadding.Bottom);
                //single capture/fullscreen
                if (_viewModel.Settings.Mode == DrawMode.RedCyanAnaglyph ||
                    _viewModel.Settings.Mode == DrawMode.GrayscaleRedCyanAnaglyph ||
                    _viewModel.Settings.FullscreenCapturing && 
                    (_viewModel.Settings.Mode == DrawMode.Cross ||
                     _viewModel.Settings.Mode == DrawMode.Parallel) ||
                    _viewModel.IsNothingCaptured && 
                    _viewModel.Settings.Mode != DrawMode.Cardboard)
                {
                    rollBounds.X = 0.5;
                    //portrait
                    if (_viewModel.DisplayOrientation == DisplayOrientation.Portrait)
                    {
                        var previewHeight = Width * _viewModel.PreviewAspectRatio;
                        var margin = (Height - previewHeight) / 2d;
                        var previewBottomY = Height - margin;
                        if (previewBottomY > apertureY)
                        {
                            rollBounds.Y = apertureY - LEVEL_ICON_WIDTH;
                        }
                        else
                        {
                            rollBounds.Y = previewBottomY - LEVEL_ICON_WIDTH;
                        }
                    }
                    //landscape
                    else
                    {
                        rollBounds.Y = Height - LEVEL_ICON_WIDTH;
                    }
                }
                //double capture/SBS
                else
                {
                    rollBounds.X = _viewModel.CameraColumn == 0 ? 0.2 : 0.8;
                    //portrait
                    if (_viewModel.DisplayOrientation == DisplayOrientation.Portrait)
                    {
                        var previewHeight = Width / 2d * _viewModel.PreviewAspectRatio;
                        var margin = (Height - previewHeight) / 2d;
                        var previewBottomY = Height - margin;
                        rollBounds.Y = previewBottomY;
                    }
                    //landscape
                    else
                    {
                        var previewHeight = Width / 2d / _viewModel.PreviewAspectRatio;
                        var margin = (Height - previewHeight) / 2d;
                        var previewBottomY = Height - margin;
                        rollBounds.Y = previewBottomY;
                    }
                }
            }
            MainThread.BeginInvokeOnMainThread(() =>
            {
                AbsoluteLayout.SetLayoutBounds(_horizontalLevelWhole, rollBounds);
            });
        }

        private void ReticlePanned(object sender, PanUpdatedEventArgs e)
	    {
	        if (e.StatusType == GestureStatus.Started)
	        {
	            _reticleLeftX = (float) _leftReticle.X;
	            _reticleY = (float) _leftReticle.Y;
	            _reticleRightX = (float) _rightReticle.X;
            }
	        else if (e.StatusType == GestureStatus.Running)
	        {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    AbsoluteLayout.SetLayoutBounds(_leftReticle, new Rect(
                    _reticleLeftX + e.TotalX,
                    _reticleY + e.TotalY,
                    RETICLE_IMAGE_WIDTH,
                    RETICLE_IMAGE_WIDTH));

                    AbsoluteLayout.SetLayoutBounds(_rightReticle, new Rect(
                    _reticleRightX + e.TotalX,
                    _reticleY + e.TotalY,
                    RETICLE_IMAGE_WIDTH,
                    RETICLE_IMAGE_WIDTH));
                });
                
            }
	        else if (e.StatusType == GestureStatus.Completed)
	        {
                var newLeftReticleBounds = AbsoluteLayout.GetLayoutBounds(_leftReticle);
                

                var newRightReticleBounds = AbsoluteLayout.GetLayoutBounds(_rightReticle);
                
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    AbsoluteLayout.SetLayoutBounds(_leftReticlePanner, new Rect(
                        newLeftReticleBounds.X - RETICLE_PANNER_WIDTH / 2d + RETICLE_IMAGE_WIDTH / 2d,
                        newLeftReticleBounds.Y - RETICLE_PANNER_WIDTH / 2d + RETICLE_IMAGE_WIDTH / 2d,
                        RETICLE_PANNER_WIDTH,
                        RETICLE_PANNER_WIDTH));
                    AbsoluteLayout.SetLayoutBounds(_rightReticlePanner, new Rect(
                        newRightReticleBounds.X - RETICLE_PANNER_WIDTH / 2d + RETICLE_IMAGE_WIDTH / 2d,
                        newRightReticleBounds.Y - RETICLE_PANNER_WIDTH / 2d + RETICLE_IMAGE_WIDTH / 2d,
                        RETICLE_PANNER_WIDTH,
                        RETICLE_PANNER_WIDTH));
                });
            }
        }

	    private void UpperLinePanned(object sender, PanUpdatedEventArgs e)
	    {
            HandleLeftLinePanEvent(e, _upperLeftLine, _upperLinePanner, ref _upperLineY, ref _upperLineHeight);
            HandleRightLinePanEvent(e, _upperRightLine, _upperLinePanner, ref _upperLineY, ref _upperLineHeight);
        }

        private void LowerLinePanned(object sender, PanUpdatedEventArgs e)
	    {
	        HandleLeftLinePanEvent(e, _lowerLeftLine, _lowerLinePanner, ref _lowerLineY, ref _lowerLineHeight);
            HandleRightLinePanEvent(e, _lowerRightLine, _lowerLinePanner, ref _lowerLineY, ref _lowerLineHeight);
        }

        private static void HandleRightLinePanEvent(PanUpdatedEventArgs e, BoxView line, ContentView panner, ref float lineY, ref float lineHeight)
        {
            HandleLinePanEvent(e, line, panner, ref lineY, ref lineHeight, false);
        }

        private static void HandleLeftLinePanEvent(PanUpdatedEventArgs e, BoxView line, ContentView panner, ref float lineY, ref float lineHeight)
        {
            HandleLinePanEvent(e, line, panner, ref lineY, ref lineHeight, true);
        }

        private static void HandleLinePanEvent(PanUpdatedEventArgs e, BoxView line, ContentView panner, 
	        ref float lineY, ref float lineHeight, bool isLeft)
	    {
	        if (e.StatusType == GestureStatus.Started)
	        {
	            var lowerLineBounds = AbsoluteLayout.GetLayoutBounds(line);
	            lineY = (float) line.Y;
	            lineHeight = (float) lowerLineBounds.Height;
	        }
	        else if (e.StatusType == GestureStatus.Running)
            {
                var y = lineY;
                var f = lineHeight;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    AbsoluteLayout.SetLayoutFlags(line, AbsoluteLayoutFlags.WidthProportional | AbsoluteLayoutFlags.XProportional);
                    AbsoluteLayout.SetLayoutBounds(line, new Rect(
                    isLeft ? 0 : 1,
	                y + e.TotalY,
	                0.5,
	                f));
                });
            }
	        else if (e.StatusType == GestureStatus.Completed)
            {
                var f = lineHeight;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    AbsoluteLayout.SetLayoutFlags(panner, AbsoluteLayoutFlags.WidthProportional | AbsoluteLayoutFlags.XProportional);
                    var newLineBounds = AbsoluteLayout.GetLayoutBounds(line);
	                AbsoluteLayout.SetLayoutBounds(panner, new Rect(
                    0,
	                newLineBounds.Y,
	                1,
	                f));
                });
            }
        }

        private void _canvas_OnTouch(object sender, SKTouchEventArgs e)
        {
            if (_viewModel == null) return;

            switch (e.ActionType)
            {
                case SKTouchAction.Entered:
                    break;
                case SKTouchAction.Pressed:
                    if (!_doubleTapTimer.Enabled)
                    {
                        _doubleTapTimer.Start();
                        _tapLocation = new PointF
                        {
                            X = e.Location.X,
                            Y = e.Location.Y
                        };
                    }
                    break;
                case SKTouchAction.Moved:
                    _dragCounter++;
                    if (_dragCounter >= MIN_MOVE_COUNTER &&
                        !_didSwap)
                    {
                        ProcessDoubleTap();
                        _viewModel?.SwapSidesCommand.Execute(null);
                        _didSwap = true;
                    }
                    break;
                case SKTouchAction.Released:
                    if (!_didSwap)
                    {
                        _releaseCounter++;
                        if (_doubleTapTimer.Enabled &&
                            _releaseCounter == 2)
                        {
                            ProcessDoubleTap();
                        }
                        else
                        {
                            ProcessSingleTap();
                        }
                    }
                    _didSwap = false;
                    break;
                case SKTouchAction.Cancelled:
                    break;
                case SKTouchAction.Exited:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            e.Handled = true;
        }

        private void TapExpired(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            ClearAllTaps();
        }

        private void ProcessDoubleTap()
        {
            _cameraModule.OnDoubleTapped();
            _viewModel.IsFocusCircleVisible = false;
            ClearAllTaps();
        }

        private void ProcessSingleTap()
        {
            if (_viewModel?.LocalPreviewFrame?.Frame == null ||
                _viewModel.Settings.Mode == DrawMode.Cardboard ||
                (_viewModel.Settings.Mode == DrawMode.RedCyanAnaglyph ||
                 _viewModel.Settings.Mode == DrawMode.GrayscaleRedCyanAnaglyph) &&
                _viewModel.Settings.IsCaptureInMirrorMode) return;

            float xProportion;
            float yProportion;
            float aspect;
            var frameHeight = _viewModel.LocalPreviewFrame.Frame.Height;
            var frameWidth = _viewModel.LocalPreviewFrame.Frame.Width;
            if (DrawTool.Orientations90deg.Contains(_viewModel.LocalPreviewFrame.Orientation))
            {
                (frameHeight, frameWidth) = (frameWidth, frameHeight);
            }
            var isPortrait = frameHeight > frameWidth;

            var fullscreenPreview = _viewModel.Settings.Mode == DrawMode.RedCyanAnaglyph || 
                                    _viewModel.Settings.Mode == DrawMode.GrayscaleRedCyanAnaglyph ||
                                    _viewModel.Settings.FullscreenCapturing ||
                                    _viewModel.Settings.FullscreenEditing ||
                                    _viewModel.IsNothingCaptured;

            if (fullscreenPreview)
            {
                double longNativeLength;
                double shortNativeLength;
                double tapLong;
                double tapShort;

                if (isPortrait)
                {
                    aspect = frameHeight / (frameWidth * 1f);
                    longNativeLength = _canvas.Height * _screenDensity;
                    shortNativeLength = _canvas.Width * _screenDensity;
                    tapLong = _tapLocation.Y;
                    tapShort = _tapLocation.X;
                }
                else
                {
                    aspect = frameWidth / (frameHeight * 1f);
                    longNativeLength = _canvas.Width * _screenDensity;
                    shortNativeLength = _canvas.Height * _screenDensity;
                    tapLong = _tapLocation.X;
                    tapShort = _tapLocation.Y;
                }

                var minLong = (longNativeLength - shortNativeLength * aspect) / 2f;

                var longProportion = (float)((tapLong - minLong) / (shortNativeLength * aspect));
                if (longProportion < 0 ||
                    longProportion > 1)
                {
                    return;
                }

                var shortProportion = (float)(tapShort / shortNativeLength);
                if (shortProportion < 0 ||
                    shortProportion > 1)
                {
                    return;
                }

                if (isPortrait)
                {
                    xProportion = shortProportion;
                    yProportion = longProportion;
                    _viewModel.FocusCircleX = shortProportion * _canvas.Width;
                    _viewModel.FocusCircleY = longProportion * (_canvas.Width * aspect) + minLong / _screenDensity;
                }
                else
                {
                    xProportion = longProportion;
                    yProportion = shortProportion;
                    _viewModel.FocusCircleY = shortProportion * _canvas.Height;
                    _viewModel.FocusCircleX = longProportion * (_canvas.Height * aspect) + minLong / _screenDensity;
                }
            }
            else
            {
                aspect = frameHeight / (frameWidth * 1f);
                var baseWidth = _canvas.Width * _screenDensity / 2f;
                var leftBufferX = _canvas.Width * _screenDensity / 2f - baseWidth;
                if (_viewModel.Settings.IsCaptureLeftFirst &&
                    _viewModel.IsNothingCaptured ||
                    !_viewModel.Settings.IsCaptureLeftFirst &&
                    _viewModel.IsExactlyOnePictureTaken)
                {
                    xProportion = (float)(_tapLocation.X / baseWidth);
                }
                else
                {
                    xProportion = (float)((_tapLocation.X - baseWidth) / baseWidth);
                    leftBufferX += baseWidth;
                }
                if (xProportion < 0 ||
                    xProportion > 1)
                {
                    return;
                }

                var baseHeight = baseWidth * aspect;
                var minY = (_canvas.Height * _screenDensity - baseHeight) / 2f;

                yProportion = (float)((_tapLocation.Y - minY) / baseHeight);
                if (yProportion < 0 ||
                    yProportion > 1)
                {
                    return;
                }

                _viewModel.FocusCircleX = (xProportion * baseWidth + leftBufferX) / _screenDensity;
                _viewModel.FocusCircleY = (yProportion * baseHeight + minY) / _screenDensity;
            }

            var convertedPoint = new PointF
            {
                X = xProportion,
                Y = yProportion
            };

            if (_viewModel.Settings.IsCaptureInMirrorMode)
            {
                if (_viewModel.Settings.Mode == DrawMode.Cross)
                {
                    if (_viewModel.Settings.IsCaptureLeftFirst)
                    {
                        if (xProportion > 0.5)
                        {
                            convertedPoint.X = 1 - xProportion;
                        }
                        else
                        {
                            convertedPoint.X = xProportion + 0.5f;
                        }
                    }
                    else
                    {
                        if (xProportion > 0.5)
                        {
                            convertedPoint.X = xProportion - 0.5f;
                        }
                        else
                        {
                            convertedPoint.X = 1 - xProportion;
                        }
                    }
                }
                else if (_viewModel.Settings.Mode == DrawMode.Parallel)
                {

                    if (_viewModel.Settings.IsCaptureLeftFirst)
                    {
                        if (xProportion > 0.5)
                        {
                            convertedPoint.X =  1.5f - xProportion;
                        }
                    }
                    else
                    {
                        if (xProportion < 0.5)
                        {
                            convertedPoint.X = 0.5f - xProportion;
                        }
                    }
                }
            }
            
            _cameraModule.OnSingleTapped(convertedPoint);
            _viewModel.IsFocusCircleVisible = true;
        }

        private void ClearAllTaps()
        {
            _doubleTapTimer.Stop();
            _releaseCounter = 0;
            _dragCounter = 0;
        }

        private void PanGestureRecognizer_OnPanUpdated(object sender, PanUpdatedEventArgs e)
        {
            //Debug.WriteLine("### Panned! Total: " + e.TotalX + "," + e.TotalY + " Status: " + e.StatusType);
            if (_viewModel.WorkflowStage != WorkflowStage.View) return;

            var xProp = e.TotalX / Width;
            var yProp = e.TotalY / Height;

            var zoomNormalizedHorizontalPan = -xProp / (1 + _viewModel.Explore.Zoom);
            var zoomNormalizedVerticalPan = -yProp / (1 + _viewModel.Explore.Zoom);

            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    break;
                case GestureStatus.Running:
                    _viewModel.Explore.Horizontal = (float)zoomNormalizedHorizontalPan;
                    _viewModel.Explore.Vertical = (float)zoomNormalizedVerticalPan;
                    break;
                case GestureStatus.Completed:
                    _viewModel.Explore.HorizontalBase =
                        Math.Clamp(_viewModel.Explore.HorizontalBase + _viewModel.Explore.Horizontal, -1, 1);
                    _viewModel.Explore.VerticalBase =
                        Math.Clamp(_viewModel.Explore.VerticalBase + _viewModel.Explore.Vertical, -1, 1);
                    _viewModel.Explore.Horizontal = 0;
                    _viewModel.Explore.Vertical = 0;
                    break;
                case GestureStatus.Canceled:
                default:
                    break;
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                _canvas.InvalidateSurface();
            });
        }

        private void PinchGestureRecognizer_OnPinchUpdated(object sender, PinchGestureUpdatedEventArgs e)
        {
            //Debug.WriteLine("### Pinched! Scale: " + e.Scale + " Status: " + e.Status + " Origin: " + e.ScaleOrigin.X + "," + e.ScaleOrigin.Y);
            if (_viewModel.WorkflowStage != WorkflowStage.View) return;

            switch (e.Status)
            {
                case GestureStatus.Started:
                    break;
                case GestureStatus.Running:
                    break;
                case GestureStatus.Completed:
                    break;
                case GestureStatus.Canceled:
                    default:
                    break;
            }

            var normalizedScale = (e.Scale - 1) / (1 + _viewModel.Explore.Zoom) / _deviceDisplayWrapper.GetDisplayDensity();

            _viewModel.Explore.Zoom = (float)Math.Clamp(_viewModel.Explore.Zoom + normalizedScale, 0,1);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _canvas.InvalidateSurface();
            });
        }
    }
}