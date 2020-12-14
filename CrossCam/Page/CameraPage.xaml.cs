using System;
using System.ComponentModel;
using System.Threading.Tasks;
using CrossCam.CustomElement;
using CrossCam.ViewModel;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using Xamarin.Essentials;
using Xamarin.Forms;

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
        private const double ACCELEROMETER_MEASURMENT_WEIGHT = 12;
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
        private double _lastAccelerometerReadingX;
        private double _lastAccelerometerReadingY;

        public CameraPage()
		{
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);

		    _horizontalLevelBubble.Source = _levelBubbleImage;
		    _horizontalLevelOutside.Source = _levelOutsideImage;

            Accelerometer.ReadingChanged += StoreAccelerometerReading;
            MessagingCenter.Subscribe<App>(this, App.APP_PAUSING_EVENT, o => EvaluateSensors(false));
		    MessagingCenter.Subscribe<App>(this, App.APP_UNPAUSING_EVENT, o => EvaluateSensors());
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
	                        Accelerometer.Start(SensorSpeed.Game);
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
	        }
        }

        private async void StartAccelerometerCycling()
        {
            while (Accelerometer.IsMonitoring)
            {
                await Task.Delay(50);
                UpdateLevelFromAccelerometerData();
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
                _averageRoll *= (ACCELEROMETER_MEASURMENT_WEIGHT - 1) / ACCELEROMETER_MEASURMENT_WEIGHT;

                if (_viewModel != null)
                {
                    if (Math.Abs(_lastAccelerometerReadingX) < Math.Abs(_lastAccelerometerReadingY)) //portrait
                    {
                        if (_viewModel.IsViewInverted)
                        {
                            _averageRoll -= _lastAccelerometerReadingX / ACCELEROMETER_MEASURMENT_WEIGHT;
                        }
                        else
                        {
                            _averageRoll += _lastAccelerometerReadingX / ACCELEROMETER_MEASURMENT_WEIGHT;
                        }
                    }
                    else //landscape
                    {
                        if (_viewModel.IsViewInverted)
                        {
                            _averageRoll += _lastAccelerometerReadingY / ACCELEROMETER_MEASURMENT_WEIGHT;
                        }
                        else
                        {
                            _averageRoll -= _lastAccelerometerReadingY / ACCELEROMETER_MEASURMENT_WEIGHT;
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
                EvaluateSensors();
                ResetLineAndDonutGuides();
                PlaceRollGuide();
            }
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
                    EvaluateSensors();
                    break;
                case nameof(CameraViewModel.Settings):
                    EvaluateSensors();
                    _drawnResultCanvas.InvalidateSurface();
                    ResetLineAndDonutGuides();
                    break;
                case nameof(CameraViewModel.CameraColumn):
                case nameof(CameraViewModel.IsCaptureLeftFirst):
                    PlaceRollGuide();
                    break;
                case nameof(CameraViewModel.IsViewPortrait):
                    _drawnResultCanvas.InvalidateSurface();
                    _pairPreviewCanvas.InvalidateSurface();
                    ResetLineAndDonutGuides();
                    SetMarginsForNotch();
                    break;
                case nameof(CameraViewModel.PreviewBottomY):
                    PlaceRollGuide();
                    PlaceSettingsRibbon();
                    break;
	            case nameof(CameraViewModel.LeftBitmap):
                case nameof(CameraViewModel.RightBitmap):
	            case nameof(CameraViewModel.RightCrop):
                case nameof(CameraViewModel.LeftCrop):
                case nameof(CameraViewModel.InsideCrop):
	            case nameof(CameraViewModel.OutsideCrop):
	            case nameof(CameraViewModel.TopCrop):
	            case nameof(CameraViewModel.BottomCrop):
                case nameof(CameraViewModel.LeftRotation):
	            case nameof(CameraViewModel.RightRotation):
                case nameof(CameraViewModel.VerticalAlignment):
                case nameof(CameraViewModel.LeftZoom):
	            case nameof(CameraViewModel.RightZoom):
	            case nameof(CameraViewModel.LeftKeystone):
	            case nameof(CameraViewModel.RightKeystone):
                    Device.BeginInvokeOnMainThread(() =>
                    {
                        _drawnResultCanvas.InvalidateSurface();
                    });
                    break;
                case nameof(CameraViewModel.PreviewFrame):
                    Device.BeginInvokeOnMainThread(() =>
                    {
                        _pairPreviewCanvas.InvalidateSurface();
                    });
                    break;
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

        private void OnPaintDrawnResult(object sender, SKPaintSurfaceEventArgs e)
	    {
	        var canvas = e.Surface.Canvas;

	        canvas.Clear(SKColor.Empty);

            DrawTool.DrawImagesOnCanvas(
                canvas, _viewModel.LeftBitmap, _viewModel.RightBitmap,
                _viewModel.Settings.BorderWidthProportion, _viewModel.Settings.AddBorder,
                _viewModel.Settings.BorderColor,
                _viewModel.LeftCrop + _viewModel.OutsideCrop, _viewModel.InsideCrop + _viewModel.RightCrop,
                _viewModel.InsideCrop + _viewModel.LeftCrop, _viewModel.RightCrop + _viewModel.OutsideCrop,
                _viewModel.TopCrop, _viewModel.BottomCrop,
                _viewModel.LeftRotation, _viewModel.RightRotation,
                _viewModel.VerticalAlignment,
                _viewModel.LeftZoom, _viewModel.RightZoom,
                _viewModel.LeftKeystone, _viewModel.RightKeystone,
                (_viewModel.Settings.Mode == DrawMode.RedCyanAnaglyph ||
                 _viewModel.Settings.Mode == DrawMode.GrayscaleRedCyanAnaglyph) && 
                _viewModel.WorkflowStage == WorkflowStage.Capture
                    ? DrawMode.Cross
                    : _viewModel.Settings.Mode);
        }

        private void OnPaintPreviewResult(object sender, SKPaintSurfaceEventArgs e)
        {
            if (_viewModel.PreviewFrame != null)
            {
                var canvas = e.Surface.Canvas;

                canvas.Clear();

                var bitmap = _viewModel.DecodeBitmapAndCorrectOrientation(_viewModel.PreviewFrame, true);

                if (bitmap != null)
                {
                    var canvasWidth = canvas.DeviceClipBounds.Width;
                    var sizeRatio = bitmap.Width / (canvasWidth * 1d);
                    var scaledHeight = bitmap.Height / sizeRatio;

                    double heightFovCorrection;
                    double widthFovCorrection;
                    var isLandscape = bitmap.Width > bitmap.Height;
                    if (_viewModel.BluetoothOperatorBindable.PartnerFov < _viewModel.BluetoothOperatorBindable.Fov)
                    {
                        if (isLandscape)
                        {
                            widthFovCorrection = -1 * CameraViewModel.FindPaddingForFovCorrection(
                                                     _viewModel.BluetoothOperatorBindable.Fov,
                                                     _viewModel.BluetoothOperatorBindable.PartnerFov,
                                                     bitmap.Width);
                            heightFovCorrection = -1 * CameraViewModel.FindPaddingForFovCorrection(
                                                      CameraViewModel.CalculatePortraitFov(_viewModel.BluetoothOperatorBindable.Fov),
                                                      CameraViewModel.CalculatePortraitFov(_viewModel.BluetoothOperatorBindable.PartnerFov),
                                                      bitmap.Height);
                        }
                        else
                        {
                            heightFovCorrection = -1 * CameraViewModel.FindPaddingForFovCorrection(
                                                      _viewModel.BluetoothOperatorBindable.Fov,
                                                      _viewModel.BluetoothOperatorBindable.PartnerFov,
                                                      bitmap.Height);
                            widthFovCorrection = -1 * CameraViewModel.FindPaddingForFovCorrection(
                                                     CameraViewModel.CalculatePortraitFov(_viewModel.BluetoothOperatorBindable.Fov),
                                                     CameraViewModel.CalculatePortraitFov(_viewModel.BluetoothOperatorBindable.PartnerFov),
                                                     bitmap.Width);
                        }
                    }
                    else
                    {
                        if (isLandscape)
                        {
                            widthFovCorrection = CameraViewModel.FindCropForFovCorrection(
                                _viewModel.BluetoothOperatorBindable.PartnerFov,
                                _viewModel.BluetoothOperatorBindable.Fov,
                                bitmap.Width);
                            heightFovCorrection = CameraViewModel.FindCropForFovCorrection(
                                CameraViewModel.CalculatePortraitFov(_viewModel.BluetoothOperatorBindable.PartnerFov),
                                CameraViewModel.CalculatePortraitFov(_viewModel.BluetoothOperatorBindable.Fov),
                                bitmap.Height);
                        }
                        else
                        {
                            heightFovCorrection = CameraViewModel.FindCropForFovCorrection(
                                _viewModel.BluetoothOperatorBindable.PartnerFov,
                                _viewModel.BluetoothOperatorBindable.Fov,
                                bitmap.Height);
                            widthFovCorrection = CameraViewModel.FindCropForFovCorrection(
                                CameraViewModel.CalculatePortraitFov(_viewModel.BluetoothOperatorBindable.PartnerFov),
                                CameraViewModel.CalculatePortraitFov(_viewModel.BluetoothOperatorBindable.Fov),
                                bitmap.Width);
                        }
                    }

                    canvas.DrawBitmap(bitmap,
                        new SKRect(
                            (float)widthFovCorrection,
                            (float)heightFovCorrection,
                            (float)(bitmap.Width - widthFovCorrection),
                            (float)(bitmap.Height - heightFovCorrection)),
                        new SKRect(
                            0,
                            (float)(canvas.DeviceClipBounds.Height-scaledHeight)/2,
                            canvas.DeviceClipBounds.Width,
                            (float)(canvas.DeviceClipBounds.Height + scaledHeight) / 2));
                }

                _viewModel.BluetoothOperatorBindable.RequestPreviewFrame();
            }
        }

        private void ResetLineAndDonutGuides()
        {
            Device.BeginInvokeOnMainThread(() =>
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
            });
        }

        private void PlaceRollGuide()
        {
            var rollBounds = AbsoluteLayout.GetLayoutBounds(_horizontalLevelWhole);
            rollBounds.Width = LEVEL_ICON_WIDTH;
            rollBounds.Height = LEVEL_ICON_WIDTH;
            rollBounds.X = _viewModel == null || _viewModel.CameraColumn == 0 ? 0.2 : 0.8;
            if (_viewModel != null)
            {
                rollBounds.Y = _viewModel.PreviewBottomY - LEVEL_ICON_WIDTH / 5;
            }
            Device.BeginInvokeOnMainThread(() =>
            {
                AbsoluteLayout.SetLayoutBounds(_horizontalLevelWhole, rollBounds);
            });
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
    }
}