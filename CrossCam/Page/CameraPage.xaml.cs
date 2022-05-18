using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using CrossCam.CustomElement;
using CrossCam.Model;
using CrossCam.ViewModel;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using Xamarin.Essentials;
using Xamarin.Forms;
using Rectangle = Xamarin.Forms.Rectangle;
using Timer = System.Timers.Timer;

namespace CrossCam.Page
{
    // ReSharper disable once UnusedMember.Global
    public partial class CameraPage
	{
	    private CameraViewModel _viewModel;

        private readonly Rectangle _upperLineBoundsLandscape = new Rectangle(0, 0.33, 1, 21);
	    private readonly Rectangle _lowerLineBoundsLandscape = new Rectangle(0, 0.67, 1, 21);
	    private readonly Rectangle _upperLineBoundsPortrait = new Rectangle(0, 0.4, 1, 21);
	    private readonly Rectangle _lowerLinesBoundsPortrait = new Rectangle(0, 0.6, 1, 21);
        
	    private const double LEVEL_ICON_WIDTH = 60;
        private const double BUBBLE_LEVEL_MAX_TIP = 0.1;
        private const double ROLL_GUIDE_MEASURMENT_WEIGHT = 12;
        private const double ROLL_GOOD_THRESHOLD = 0.01;
        private readonly ImageSource _levelBubbleImage = ImageSource.FromFile("horizontalLevelInside");
	    private readonly ImageSource _levelOutsideImage = ImageSource.FromFile("horizontalLevelOutside");
	    private readonly ImageSource _levelBubbleGreenImage = ImageSource.FromFile("horizontalLevelInsideGreen");
	    private readonly ImageSource _levelOutsideGreenImage = ImageSource.FromFile("horizontalLevelOutsideGreen");

        private const double RETICLE_WIDTH = 30;
        private readonly Rectangle _leftReticleBounds = new Rectangle(0.5, 0.5, RETICLE_WIDTH, RETICLE_WIDTH);
        private readonly Rectangle _rightReticleBounds = new Rectangle(0.5, 0.5, RETICLE_WIDTH, RETICLE_WIDTH);

        public const double FOCUS_CIRCLE_WIDTH = 30;

	    private double _reticleLeftX;
	    private double _reticleRightX;
	    private double _reticleY;

	    private double _upperLineY;
	    private double _upperLineHeight;

	    private double _lowerLineY;
	    private double _lowerLineHeight;

        private double _averageRoll;

        private Stopwatch _gyroscopeStopwatch;
        private double _cardboardViewVert;
        private double _cardboardViewHor;

        private double _lastAccelerometerReadingX;
        private double _lastAccelerometerReadingY;
        
        private double? _cardboardHomeVert;
        private double? _cardboardHomeHor;

        private bool _newLeftCapture;
        private bool _newRightCapture;

        private const SensorSpeed SENSOR_SPEED = SensorSpeed.Fastest;
        private const int SENSOR_FRAME_DELAY = 10;

        public CameraPage()
		{
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);

		    _horizontalLevelBubble.Source = _levelBubbleImage;
		    _horizontalLevelOutside.Source = _levelOutsideImage;

            Accelerometer.ReadingChanged += StoreAccelerometerReading;
            Gyroscope.ReadingChanged += StoreGyroscopeReading;
            _gyroscopeStopwatch = new Stopwatch();
            MessagingCenter.Subscribe<App>(this, App.APP_PAUSING_EVENT, o => EvaluateSensors(false));
		    MessagingCenter.Subscribe<App>(this, App.APP_UNPAUSING_EVENT, o => EvaluateSensors());
            _doubleTapTimer.Elapsed += (sender, args) =>
            {
                TapExpired();
            };
        }

        protected override bool OnBackButtonPressed()
	    {
	        return _viewModel?.BackButtonPressed() ?? base.OnBackButtonPressed();
	    }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            SetMarginsForNotch();
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
	                    catch
	                    {
                            //oh well
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
                        catch
                        {
                            //oh well
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
                var seconds = _gyroscopeStopwatch.ElapsedTicks / 10000000d;
                _cardboardViewVert -= e.Reading.AngularVelocity.Y * seconds;
                _cardboardViewHor += e.Reading.AngularVelocity.X * seconds;
                _gyroscopeStopwatch.Restart();
                Device.BeginInvokeOnMainThread(() =>
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
            Device.BeginInvokeOnMainThread(() =>
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

                    if (Math.Abs(_averageRoll) < ROLL_GOOD_THRESHOLD &&
                        _horizontalLevelBubble.Source == _levelBubbleImage)
                    {
                        _horizontalLevelBubble.Source = _levelBubbleGreenImage;
                        _horizontalLevelOutside.Source = _levelOutsideGreenImage;
                    }
                    else if (Math.Abs(_averageRoll) > ROLL_GOOD_THRESHOLD &&
                            _horizontalLevelBubble.Source == _levelBubbleGreenImage)
                    {
                        _horizontalLevelBubble.Source = _levelBubbleImage;
                        _horizontalLevelOutside.Source = _levelOutsideImage;
                    }
                }

                var newX = 0.5 + _averageRoll / BUBBLE_LEVEL_MAX_TIP / 2;
                if (newX > 1)
                {
                    newX = 1;
                } else if (newX < 0)
                {
                    newX = 0;
                }
                var bubbleBounds = AbsoluteLayout.GetLayoutBounds(_horizontalLevelBubble);
                bubbleBounds.X = newX;
                AbsoluteLayout.SetLayoutBounds(_horizontalLevelBubble, bubbleBounds);
            });
        }

        protected override void OnBindingContextChanged()
	    {
	        base.OnBindingContextChanged();
	        if (BindingContext != null)
	        {
	            _viewModel = (CameraViewModel) BindingContext;
	            _viewModel.PropertyChanged += ViewModelPropertyChanged;
                _viewModel.Edits.PropertyChanged += EditsPropertyChanged;
                Device.BeginInvokeOnMainThread(() =>
                {
                    var layout = AbsoluteLayout.GetLayoutBounds(_doubleMoveHintStack);
                    layout.Width = _viewModel.Settings.CardboardIpd + 100;
                    AbsoluteLayout.SetLayoutBounds(_doubleMoveHintStack, layout);

                    EvaluateSensors();
                    ResetLineAndDonutGuides();
                    PlaceRollGuide();
                });
            }
	    }

        private void EditsPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                _canvas.InvalidateSurface();
            });
        }

        private void ViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
	    {
            Device.BeginInvokeOnMainThread(() =>
            {
                switch (e.PropertyName)
                {
                    case nameof(CameraViewModel.FocusCircleX):
                    case nameof(CameraViewModel.FocusCircleY):
                        MoveFocusCircle();
                        break;
                    case nameof(CameraViewModel.WorkflowStage):
                        EvaluateSensors();
                        break;
                    case nameof(CameraViewModel.Settings):
                        EvaluateSensors();
                        _canvas.InvalidateSurface();
                        ResetLineAndDonutGuides();
                        break;
                    case nameof(CameraViewModel.CameraColumn):
                    case nameof(Settings.IsCaptureLeftFirst):
                        PlaceRollGuide();
                        break;
                    case nameof(CameraViewModel.IsViewPortrait):
                        _canvas.InvalidateSurface();
                        ResetLineAndDonutGuides();
                        SetMarginsForNotch();
                        SwapSidesIfCardboard();
                        break;
                    case nameof(CameraViewModel.PreviewBottomY):
                        PlaceRollGuide();
                        PlaceSettingsRibbon();
                        break;
                    case nameof(CameraViewModel.LeftOrientation):
                    case nameof(CameraViewModel.LeftBitmap):
                    case nameof(CameraViewModel.LeftAlignmentTransform):
                        ProcessDoubleTap();
                        _newLeftCapture = true;
                        CardboardCheckAndSaveOrientationSnapshot();
                        _canvas.InvalidateSurface();
                        break;
                    case nameof(CameraViewModel.RightOrientation):
                    case nameof(CameraViewModel.RightBitmap):
                    case nameof(CameraViewModel.RightAlignmentTransform):
                        ProcessDoubleTap();
                        _newRightCapture = true;
                        CardboardCheckAndSaveOrientationSnapshot();
                        _canvas.InvalidateSurface();
                        break;
                    case nameof(CameraViewModel.RemotePreviewFrame):
                    case nameof(CameraViewModel.LocalPreviewFrame):
                        _canvas.InvalidateSurface();
                        break;
                }
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

        private void SwapSidesIfCardboard()
        {
            if (_viewModel.Settings.Mode == DrawMode.Cardboard &&
                !_viewModel.IsViewPortrait)
            {
                switch (_viewModel.WorkflowStage)
                {
                    case WorkflowStage.Capture:
                        _viewModel?.SwapSidesCommand.Execute(null);
                        break;
                    case WorkflowStage.Final:
                        _viewModel?.ClearCapturesCommand.Execute(null);
                        break;
                }
            }
        }

        private void PlaceSettingsRibbon()
        {
            if (_viewModel != null)
            {
                var originalBounds = AbsoluteLayout.GetLayoutBounds(_cameraSettingsRibbon);
                originalBounds.Y = _viewModel.PreviewBottomY - originalBounds.Height;
                AbsoluteLayout.SetLayoutBounds(_cameraSettingsRibbon, originalBounds);
            }
        }

        private void MoveFocusCircle()
        {
            if (_viewModel != null)
            {
                AbsoluteLayout.SetLayoutBounds(_focusCircle, new Rectangle
                {
                    X = _viewModel.FocusCircleX - FOCUS_CIRCLE_WIDTH / 2d,
                    Y = _viewModel.FocusCircleY - FOCUS_CIRCLE_WIDTH / 2d,
                    Width = FOCUS_CIRCLE_WIDTH,
                    Height = FOCUS_CIRCLE_WIDTH
                });
            }
        }

        private void OnCanvasInvalidated(object sender, SKPaintSurfaceEventArgs e)
	    {
            var surface = e.Surface;

            var useOverlay = _viewModel.Settings.Mode == DrawMode.RedCyanAnaglyph ||
                              _viewModel.Settings.Mode == DrawMode.GrayscaleRedCyanAnaglyph ||
                              _viewModel.Settings.ShowGhostCaptures;

            if (_viewModel.LeftBitmap == null &&
                _viewModel.RightBitmap == null)
            {
                surface.Canvas.Clear();
                _cardboardHomeHor = null;
                _cardboardHomeVert = null;
            }

            if (useOverlay)
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
                leftOrientation = _viewModel.LeftOrientation;
                isLeftFrontFacing = _viewModel.IsLeftFrontFacing;
                right = _viewModel.RightBitmap;
                rightAlignment = _viewModel.RightAlignmentTransform;
                rightOrientation = _viewModel.RightOrientation;
                isRightFrontFacing = _viewModel.IsRightFrontFacing;
            }
            else
            {
                if (_newLeftCapture || 
                    useOverlay &&
                    _viewModel.LeftBitmap != null)
                {
                    left = _viewModel.LeftBitmap;
                    leftAlignment = _viewModel.LeftAlignmentTransform;
                    leftOrientation = _viewModel.LeftOrientation;
                    isLeftFrontFacing = _viewModel.IsLeftFrontFacing;
                    _newLeftCapture = false;
                }
                else if (_viewModel.CameraColumn == 0)
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
                    useOverlay &&
                    _viewModel.RightBitmap != null)
                {
                    right = _viewModel.RightBitmap;
                    rightAlignment = _viewModel.RightAlignmentTransform;
                    rightOrientation = _viewModel.RightOrientation;
                    isRightFrontFacing = _viewModel.IsRightFrontFacing;
                    _newRightCapture = false;
                }
                else if (_viewModel.CameraColumn == 1)
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

            if (_viewModel.Settings.Mode == DrawMode.Cardboard)
            {
                if (_viewModel.LeftBitmap == null &&
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
            }

            double cardboardVert = 0;
            if (_cardboardHomeVert.HasValue)
            {
                if (_viewModel.IsViewInverted)
                {
                    cardboardVert = _cardboardViewVert - _cardboardHomeVert.Value;
                    if (cardboardVert > 0.5)
                    {
                        _cardboardHomeVert = _cardboardHomeVert.Value + (cardboardVert - 0.5);
                        cardboardVert = 0.5;
                    }
                    else if (cardboardVert < -0.5)
                    {
                        _cardboardHomeVert = _cardboardHomeVert.Value + (cardboardVert + 0.5);
                        cardboardVert = -0.5;
                    }
                }
                else
                {
                    cardboardVert = _cardboardHomeVert.Value - _cardboardViewVert;
                    if (cardboardVert > 0.5)
                    {
                        _cardboardHomeVert = _cardboardHomeVert.Value - (cardboardVert - 0.5);
                        cardboardVert = 0.5;
                    }
                    else if (cardboardVert < -0.5)
                    {
                        _cardboardHomeVert = _cardboardHomeVert.Value - (cardboardVert + 0.5);
                        cardboardVert = -0.5;
                    }
                }
            }

            double cardboardHor = 0;
            if (_cardboardHomeHor.HasValue)
            {
                if (_viewModel.IsViewInverted)
                {
                    cardboardHor = _cardboardViewHor - _cardboardHomeHor.Value;
                    if (cardboardHor > 0.5)
                    {
                        _cardboardHomeHor = _cardboardHomeHor.Value + (cardboardHor - 0.5);
                        cardboardHor = 0.5;
                    }
                    else if (cardboardHor < -0.5)
                    {
                        _cardboardHomeHor = _cardboardHomeHor.Value + (cardboardHor + 0.5);
                        cardboardHor = -0.5;
                    }
                }
                else
                {
                    cardboardHor = _cardboardHomeHor.Value - _cardboardViewHor;
                    if (cardboardHor > 0.5)
                    {
                        _cardboardHomeHor = _cardboardHomeHor.Value - (cardboardHor - 0.5);
                        cardboardHor = 0.5;
                    }
                    else if (cardboardHor < -0.5)
                    {
                        _cardboardHomeHor = _cardboardHomeHor.Value - (cardboardHor + 0.5);
                        cardboardHor = -0.5;
                    }
                }
            }

            DrawTool.DrawImagesOnCanvas(
                surface, 
                left, leftAlignment, leftOrientation ?? SKEncodedOrigin.Default, isLeftFrontFacing,
                right, rightAlignment, rightOrientation ?? SKEncodedOrigin.Default, isRightFrontFacing,
                _viewModel.Settings,
                _viewModel.Edits,
                _viewModel.Settings.Mode, 
                _viewModel.PairOperatorBindable.PairStatus == PairStatus.Connected || _viewModel.WasCapturePaired,
                drawQuality: _viewModel.IsExactlyOnePictureTaken || 
                             _viewModel.IsNothingCaptured ? 
                    DrawQuality.Preview : DrawQuality.Review,
                cardboardVert: cardboardVert,
                cardboardHor: cardboardHor);

            if (_viewModel.PairOperatorBindable.PairStatus == PairStatus.Connected)
            {
                _viewModel.PairOperatorBindable.RequestPreviewFrame();
            }
        }

        private void ResetLineAndDonutGuides()
        {
            if (_viewModel == null || _viewModel.IsViewPortrait)
            {
                AbsoluteLayout.SetLayoutFlags(_upperLine,
                    AbsoluteLayoutFlags.YProportional | AbsoluteLayoutFlags.WidthProportional);
                AbsoluteLayout.SetLayoutBounds(_upperLine, _upperLineBoundsPortrait);
                AbsoluteLayout.SetLayoutFlags(_upperLinePanner,
                    AbsoluteLayoutFlags.YProportional | AbsoluteLayoutFlags.WidthProportional);
                AbsoluteLayout.SetLayoutBounds(_upperLinePanner, _upperLineBoundsPortrait);

                AbsoluteLayout.SetLayoutFlags(_lowerLine,
                    AbsoluteLayoutFlags.YProportional | AbsoluteLayoutFlags.WidthProportional);
                AbsoluteLayout.SetLayoutBounds(_lowerLine, _lowerLinesBoundsPortrait);
                AbsoluteLayout.SetLayoutFlags(_lowerLinePanner,
                    AbsoluteLayoutFlags.YProportional | AbsoluteLayoutFlags.WidthProportional);
                AbsoluteLayout.SetLayoutBounds(_lowerLinePanner, _lowerLinesBoundsPortrait);
            }
            else
            {
                AbsoluteLayout.SetLayoutFlags(_upperLine,
                    AbsoluteLayoutFlags.YProportional | AbsoluteLayoutFlags.WidthProportional);
                AbsoluteLayout.SetLayoutBounds(_upperLine, _upperLineBoundsLandscape);
                AbsoluteLayout.SetLayoutFlags(_upperLinePanner,
                    AbsoluteLayoutFlags.YProportional | AbsoluteLayoutFlags.WidthProportional);
                AbsoluteLayout.SetLayoutBounds(_upperLinePanner, _upperLineBoundsLandscape);

                AbsoluteLayout.SetLayoutFlags(_lowerLine,
                    AbsoluteLayoutFlags.YProportional | AbsoluteLayoutFlags.WidthProportional);
                AbsoluteLayout.SetLayoutBounds(_lowerLine, _lowerLineBoundsLandscape);
                AbsoluteLayout.SetLayoutFlags(_lowerLinePanner,
                    AbsoluteLayoutFlags.YProportional | AbsoluteLayoutFlags.WidthProportional);
                AbsoluteLayout.SetLayoutBounds(_lowerLinePanner, _lowerLineBoundsLandscape);
            }

            AbsoluteLayout.SetLayoutFlags(_leftReticle, AbsoluteLayoutFlags.PositionProportional);
            AbsoluteLayout.SetLayoutBounds(_leftReticle, _leftReticleBounds);
            AbsoluteLayout.SetLayoutFlags(_leftReticlePanner, AbsoluteLayoutFlags.PositionProportional);
            AbsoluteLayout.SetLayoutBounds(_leftReticlePanner, _leftReticleBounds);

            AbsoluteLayout.SetLayoutFlags(_rightReticle, AbsoluteLayoutFlags.PositionProportional);
            AbsoluteLayout.SetLayoutBounds(_rightReticle, _rightReticleBounds);
            AbsoluteLayout.SetLayoutFlags(_rightReticlePanner, AbsoluteLayoutFlags.PositionProportional);
            AbsoluteLayout.SetLayoutBounds(_rightReticlePanner, _rightReticleBounds);
        }

        private void PlaceRollGuide()
        {
            var rollBounds = AbsoluteLayout.GetLayoutBounds(_horizontalLevelWhole);
            rollBounds.Width = LEVEL_ICON_WIDTH;
            rollBounds.Height = LEVEL_ICON_WIDTH;
            if (_viewModel == null)
            {
                rollBounds.X = 0.2;
            }
            else
            {
                if (_viewModel.Settings.Mode == DrawMode.RedCyanAnaglyph ||
                    _viewModel.Settings.Mode == DrawMode.GrayscaleRedCyanAnaglyph ||
                    _viewModel.Settings.ShowGhostCaptures && 
                    (_viewModel.Settings.Mode == DrawMode.Cross ||
                     _viewModel.Settings.Mode == DrawMode.Parallel))
                {
                    rollBounds.X = 0.5;
                }
                else
                {
                    rollBounds.X = _viewModel.CameraColumn == 0 ? 0.2 : 0.8;
                }
                rollBounds.Y = _viewModel.PreviewBottomY - LEVEL_ICON_WIDTH / 5;
            }
            AbsoluteLayout.SetLayoutBounds(_horizontalLevelWhole, rollBounds);

            var leftFuseGuideBounds = AbsoluteLayout.GetLayoutBounds(_leftFuseGuide);
            var rightFuseGuideBounds = AbsoluteLayout.GetLayoutBounds(_rightFuseGuide);
            if (_viewModel != null)
            {
                var previewY = Height - _viewModel.PreviewBottomY;
                var previewHeight = (float)(_viewModel.PreviewBottomY - previewY);
                var iconWidth = DrawTool.CalculateFuseGuideWidth(previewHeight);
                leftFuseGuideBounds.Width = iconWidth;
                leftFuseGuideBounds.Height = iconWidth;
                rightFuseGuideBounds.Width = iconWidth;
                rightFuseGuideBounds.Height = iconWidth;
                var fuseGuideY = previewY - DrawTool.CalculateFuseGuideMarginHeight(previewHeight) / 2d - iconWidth / 2d; 
                leftFuseGuideBounds.Y = fuseGuideY;
                rightFuseGuideBounds.Y = fuseGuideY;
                leftFuseGuideBounds.X = Width / 2d - _previewGrid.Width / 4d;
                rightFuseGuideBounds.X = Width / 2d + _previewGrid.Width / 4d;
                AbsoluteLayout.SetLayoutBounds(_leftFuseGuide, leftFuseGuideBounds);
                AbsoluteLayout.SetLayoutBounds(_rightFuseGuide, rightFuseGuideBounds);
            }
        }

        private void ReticlePanned(object sender, PanUpdatedEventArgs e)
	    {
	        if (e.StatusType == GestureStatus.Started)
	        {
	            _reticleLeftX = _leftReticle.X;
	            _reticleY = _leftReticle.Y;
	            _reticleRightX = _rightReticle.X;
            }
	        else if (e.StatusType == GestureStatus.Running)
	        {
	            if (AbsoluteLayout.GetLayoutFlags(_leftReticle) != AbsoluteLayoutFlags.None)
	            {
	                AbsoluteLayout.SetLayoutFlags(_leftReticle, AbsoluteLayoutFlags.None);
	                AbsoluteLayout.SetLayoutFlags(_rightReticle, AbsoluteLayoutFlags.None);
	            }

                AbsoluteLayout.SetLayoutBounds(_leftReticle, new Rectangle(
	                _reticleLeftX + e.TotalX - RETICLE_WIDTH/2d,
	                _reticleY + e.TotalY - RETICLE_WIDTH / 2d,
	                RETICLE_WIDTH,
	                RETICLE_WIDTH));

	            AbsoluteLayout.SetLayoutBounds(_rightReticle, new Rectangle(
	                _reticleRightX + e.TotalX - RETICLE_WIDTH / 2d,
	                _reticleY + e.TotalY - RETICLE_WIDTH / 2d,
	                RETICLE_WIDTH,
	                RETICLE_WIDTH));
            }
	        else if (e.StatusType == GestureStatus.Completed)
	        {
	            if (AbsoluteLayout.GetLayoutFlags(_leftReticlePanner) != AbsoluteLayoutFlags.None)
	            {
	                AbsoluteLayout.SetLayoutFlags(_leftReticlePanner, AbsoluteLayoutFlags.None);
	                AbsoluteLayout.SetLayoutFlags(_rightReticlePanner, AbsoluteLayoutFlags.None);
	            }

	            var newLeftReticleBounds = AbsoluteLayout.GetLayoutBounds(_leftReticle);
	            AbsoluteLayout.SetLayoutBounds(_leftReticlePanner, new Rectangle(
	                newLeftReticleBounds.X,
	                newLeftReticleBounds.Y,
	                RETICLE_WIDTH,
	                RETICLE_WIDTH));

	            var newRightReticleBounds = AbsoluteLayout.GetLayoutBounds(_rightReticle);
	            AbsoluteLayout.SetLayoutBounds(_rightReticlePanner, new Rectangle(
	                newRightReticleBounds.X,
	                newRightReticleBounds.Y,
	                RETICLE_WIDTH,
	                RETICLE_WIDTH));
	        }
        }

	    private void UpperLinePanned(object sender, PanUpdatedEventArgs e)
	    {
            HandleLinePanEvent(e, _upperLine, _upperLinePanner, ref _upperLineY, ref _upperLineHeight);
	    }

	    private void LowerLinePanned(object sender, PanUpdatedEventArgs e)
	    {
	        HandleLinePanEvent(e, _lowerLine, _lowerLinePanner, ref _lowerLineY, ref _lowerLineHeight);
	    }

	    private static void HandleLinePanEvent(PanUpdatedEventArgs e, BoxView line, ContentView panner, 
	        ref double lineY, ref double lineHeight)
	    {
	        if (e.StatusType == GestureStatus.Started)
	        {
	            var lowerLineBounds = AbsoluteLayout.GetLayoutBounds(line);
	            lineY = line.Y;
	            lineHeight = lowerLineBounds.Height;
	        }
	        else if (e.StatusType == GestureStatus.Running)
	        {
	            if (AbsoluteLayout.GetLayoutFlags(line) != AbsoluteLayoutFlags.WidthProportional)
	            {
	                AbsoluteLayout.SetLayoutFlags(line, AbsoluteLayoutFlags.WidthProportional);
	            }

                AbsoluteLayout.SetLayoutBounds(line, new Rectangle(
	                0,
	                lineY + e.TotalY,
	                1,
	                lineHeight));
	        }
	        else if (e.StatusType == GestureStatus.Completed)
	        {
	            if (AbsoluteLayout.GetLayoutFlags(panner) != AbsoluteLayoutFlags.WidthProportional)
	            {
	                AbsoluteLayout.SetLayoutFlags(panner, AbsoluteLayoutFlags.WidthProportional);
	            }

	            var newLineBounds = AbsoluteLayout.GetLayoutBounds(line);
	            AbsoluteLayout.SetLayoutBounds(panner, new Rectangle(
	                0,
	                newLineBounds.Y,
	                1,
	                lineHeight));
            }
        }

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

        private void TapExpired()
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
            if (_viewModel?.LocalPreviewFrame.Frame == null || 
                _viewModel.Settings.Mode == DrawMode.Cardboard) return;

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

            var overlayDisplay = _viewModel.Settings.Mode == DrawMode.RedCyanAnaglyph ||
                                 _viewModel.Settings.Mode == DrawMode.GrayscaleRedCyanAnaglyph ||
                                 _viewModel.Settings.ShowGhostCaptures;

            if (overlayDisplay)
            {
                double longNativeLength;
                double shortNativeLength;
                double tapLong;
                double tapShort;

                if (isPortrait)
                {
                    aspect = frameHeight / (frameWidth * 1f);
                    longNativeLength = Height * DeviceDisplay.MainDisplayInfo.Density;
                    shortNativeLength = Width * DeviceDisplay.MainDisplayInfo.Density;
                    tapLong = _tapLocation.Y;
                    tapShort = _tapLocation.X;
                }
                else
                {
                    aspect = frameWidth / (frameHeight * 1f);
                    longNativeLength = Width * DeviceDisplay.MainDisplayInfo.Density;
                    shortNativeLength = Height * DeviceDisplay.MainDisplayInfo.Density;
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
                double shortProportion = (float)(tapShort / shortNativeLength);

                if (isPortrait)
                {
                    xProportion = (float)shortProportion;
                    yProportion = longProportion;
                    _viewModel.FocusCircleX = shortProportion * Width;
                    _viewModel.FocusCircleY = longProportion * (Width * aspect) + minLong / DeviceDisplay.MainDisplayInfo.Density;
                }
                else
                {
                    xProportion = longProportion;
                    yProportion = (float)shortProportion;
                    _viewModel.FocusCircleY = shortProportion * Height;
                    _viewModel.FocusCircleX = longProportion * (Height * aspect) + minLong / DeviceDisplay.MainDisplayInfo.Density;
                }
            }
            else
            {
                aspect = frameHeight / (frameWidth * 1f);
                double baseWidth;
                if (_viewModel.Settings.Mode == DrawMode.Cross)
                {
                    baseWidth = Width * DeviceDisplay.MainDisplayInfo.Density / 2f;
                }
                else
                {
                    baseWidth = _viewModel.Settings.MaximumParallelWidth * DeviceDisplay.MainDisplayInfo.Density / 2f;
                }
                var leftBufferX = Width * DeviceDisplay.MainDisplayInfo.Density / 2f - baseWidth;
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
                var minY = (Height * DeviceDisplay.MainDisplayInfo.Density - baseHeight) / 2f;

                yProportion = (float)((_tapLocation.Y - minY) / baseHeight);
                if (yProportion < 0 ||
                    yProportion > 1)
                {
                    return;
                }

                _viewModel.FocusCircleX = (xProportion * baseWidth + leftBufferX) / DeviceDisplay.MainDisplayInfo.Density;
                _viewModel.FocusCircleY = (yProportion * baseHeight + minY) / DeviceDisplay.MainDisplayInfo.Density;
            }

            var convertedPoint = new PointF
            {
                X = xProportion,
                Y = yProportion
            };
            
            _cameraModule.OnSingleTapped(convertedPoint);
            _viewModel.IsFocusCircleVisible = true;
        }

        private void ClearAllTaps()
        {
            _doubleTapTimer.Stop();
            _releaseCounter = 0;
            _dragCounter = 0;
        }
    }
}