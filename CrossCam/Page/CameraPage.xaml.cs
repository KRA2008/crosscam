using System;
using System.ComponentModel;
using CrossCam.ViewModel;
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

	    private const double ROTATION_GUIDES_PORTRAIT_Y = 0.7;
	    private const double ROTATION_GUIDES_LANDSCAPE_Y = 0.9;
        
	    private const double LEVEL_BUBBLE_MIDDLE = 21.5;
	    private const double LEVEL_BUBBLE_SPEED = 5;

	    private readonly Rectangle _leftReticleBounds = new Rectangle(0.2297, 0.5, 0.075, 0.075);
        private readonly Rectangle _rightReticleBounds = new Rectangle(0.7703, 0.5, 0.075, 0.075);

	    private double _reticleLeftX;
	    private double _reticleRightX;
	    private double _reticleY;
	    private double _reticleWidth;

	    private double _upperLineY;
	    private double _upperLineHeight;

	    private double _lowerLineY;
	    private double _lowerLineHeight;

	    private const double ACCELEROMETER_MEASURMENT_WEIGHT = 12;
	    private const double ACCELEROMETER_SENSITIVITY = 90;
	    private const double AVERAGE_ROLL_LIMIT = LEVEL_BUBBLE_MIDDLE / (ACCELEROMETER_SENSITIVITY * LEVEL_BUBBLE_SPEED);

        private double _averageRoll;

	    private double _averagePitch;
	    private double _firstPhotoPitch;
	    private readonly ImageSource _pitchForward = ImageSource.FromFile("rotateForwardInBoxWall");
	    private readonly ImageSource _pitchBackward = ImageSource.FromFile("rotateBackwardInBoxWall");
	    private readonly ImageSource _pitchStar = ImageSource.FromFile("starInBoxWall");

	    private const double COMPASS_MEASURMENT_WEIGHT = 2;
	    private const double COMPASS_SENSITIVITY = 0.9;

        private double _averageYaw;
	    private double _firstPhotoYaw;
	    private readonly ImageSource _yawLeft = ImageSource.FromFile("rotateLeftInBoxFloor");
	    private readonly ImageSource _yawRight = ImageSource.FromFile("rotateRightInBoxFloor");
	    private readonly ImageSource _yawStar = ImageSource.FromFile("starInBoxFloor");

        public CameraPage()
		{
            InitializeComponent();
            ResetGuides();
		    NavigationPage.SetHasNavigationBar(this, false);

		    var bubbleBounds = AbsoluteLayout.GetLayoutBounds(_horizontalLevelBubble);
		    bubbleBounds.X = LEVEL_BUBBLE_MIDDLE;
		    AbsoluteLayout.SetLayoutBounds(_horizontalLevelBubble, bubbleBounds);

            Accelerometer.ReadingChanged += HandleAccelerometerReading;
		    Compass.ReadingChanged += HandleCompassReading;
            MessagingCenter.Subscribe<App>(this, App.APP_PAUSING_EVENT, o => EvaluateSensors(false));
		    MessagingCenter.Subscribe<App>(this, App.APP_UNPAUSING_EVENT, o => EvaluateSensors());
        }

	    private void EvaluateSensors(bool isAppRunning = true)
	    {
	        if (_viewModel != null)
	        {
	            if ((_viewModel.Settings.ShowPitchGuide || _viewModel.Settings.ShowRollGuide) && 
	                _viewModel.WorkflowStage == WorkflowStage.Capture &&
	                isAppRunning)
	            {
	                if (!Accelerometer.IsMonitoring)
	                {
	                    try
	                    {
	                        Accelerometer.Start(SensorSpeed.Game);
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

	            if (_viewModel.Settings.ShowYawGuide &&
	                _viewModel.WorkflowStage == WorkflowStage.Capture &&
	                isAppRunning)
	            {
	                if (!Compass.IsMonitoring)
	                {
	                    try
	                    {
                            Compass.Start(SensorSpeed.Fastest);
	                    }
	                    catch
	                    {
                            //oh well
	                    }
                    }
	            }
	            else
	            {
	                if (Compass.IsMonitoring)
	                {
	                    try
	                    {
                            Compass.Stop();
	                    }
	                    catch
	                    {
                            //oh well
	                    }
	                }
	            }
	        }
	    }

        private void HandleCompassReading(object sender, CompassChangedEventArgs args)
        {
            _averageYaw *= (COMPASS_MEASURMENT_WEIGHT - 1) / COMPASS_MEASURMENT_WEIGHT;

            var heading = args.Reading.HeadingMagneticNorth;
            if (_viewModel != null && _viewModel.IsNothingCaptured)
            {
                _firstPhotoYaw = heading;
            }
            if (_viewModel != null && _viewModel.IsExactlyOnePictureTaken)
            {
                _averageYaw += heading / COMPASS_MEASURMENT_WEIGHT;
            }

            var roundedYawDifference = Math.Round((_firstPhotoYaw - _averageYaw) * COMPASS_SENSITIVITY);
            if (roundedYawDifference > 0)
            {
                _yawIndicator.Source = _yawRight;
            }
            else if (roundedYawDifference < 0)
            {
                _yawIndicator.Source = _yawLeft;
            }
            else
            {
                _yawIndicator.Source = _yawStar;
            }
        }

        private void HandleAccelerometerReading(object sender, AccelerometerChangedEventArgs args)
        {
            _averageRoll *= (ACCELEROMETER_MEASURMENT_WEIGHT - 1) / ACCELEROMETER_MEASURMENT_WEIGHT;
            _averagePitch *= (ACCELEROMETER_MEASURMENT_WEIGHT - 1) / ACCELEROMETER_MEASURMENT_WEIGHT;

            var acceleration = args.Reading.Acceleration;
            if (_viewModel != null && 
                _viewModel.IsNothingCaptured)
            {
                _firstPhotoPitch = acceleration.Z;
            }
            if (_viewModel != null && 
                _viewModel.IsExactlyOnePictureTaken)
            {
                _averagePitch += acceleration.Z / ACCELEROMETER_MEASURMENT_WEIGHT;
            }

            if (_viewModel != null)
            {
                if (Math.Abs(acceleration.X) < Math.Abs(acceleration.Y)) //portrait
                {
                    if (_viewModel.IsViewInverted)
                    {
                        _averageRoll -= acceleration.X / ACCELEROMETER_MEASURMENT_WEIGHT;
                    }
                    else
                    {
                        _averageRoll += acceleration.X / ACCELEROMETER_MEASURMENT_WEIGHT;
                    }
                }
                else //landscape
                {
                    if (_viewModel.IsViewInverted)
                    {
                        _averageRoll += acceleration.Y / ACCELEROMETER_MEASURMENT_WEIGHT;
                    }
                    else
                    {
                        _averageRoll -= acceleration.Y / ACCELEROMETER_MEASURMENT_WEIGHT;
                    }
                }
            }

            if (_averageRoll > AVERAGE_ROLL_LIMIT)
            {
                _averageRoll = AVERAGE_ROLL_LIMIT;
            } else if (_averageRoll < -AVERAGE_ROLL_LIMIT)
            {
                _averageRoll = -AVERAGE_ROLL_LIMIT;
            }
            var bubbleBounds = AbsoluteLayout.GetLayoutBounds(_horizontalLevelBubble);
            var newBounds = LEVEL_BUBBLE_MIDDLE + _averageRoll * LEVEL_BUBBLE_SPEED * ACCELEROMETER_SENSITIVITY;
            if (newBounds < 0)
            {
                newBounds = 0;
            } else if (newBounds > LEVEL_BUBBLE_MIDDLE * 2)
            {
                newBounds = LEVEL_BUBBLE_MIDDLE * 2;
            }
            bubbleBounds.X = newBounds;
            AbsoluteLayout.SetLayoutBounds(_horizontalLevelBubble, bubbleBounds);

            var roundedPitchDifference = Math.Round((_firstPhotoPitch - _averagePitch) * ACCELEROMETER_SENSITIVITY);
            if (roundedPitchDifference > 0)
            {
                _pitchIndicator.Source = _pitchForward;
            }
            else if (roundedPitchDifference < 0)
            {
                _pitchIndicator.Source = _pitchBackward;
            }
            else
            {
                _pitchIndicator.Source = _pitchStar;
            }
        }

        protected override void OnBindingContextChanged()
	    {
	        base.OnBindingContextChanged();
	        if (BindingContext != null)
	        {
	            _viewModel = (CameraViewModel) BindingContext;
	            _viewModel.PropertyChanged += ViewModelPropertyChanged;
                EvaluateSensors();
	        }
	    }

	    private void ViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
	    {
	        switch (e.PropertyName)
	        {
                case nameof(CameraViewModel.WorkflowStage):
                    EvaluateSensors();
                    break;
                case nameof(CameraViewModel.Settings):
                    EvaluateSensors();
                    _canvasView.InvalidateSurface();
                    ResetGuides();
                    break;
                case nameof(CameraViewModel.IsViewPortrait):
                case nameof(CameraViewModel.CameraColumn):
	                ResetGuides();
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
                    _canvasView.InvalidateSurface();
                    break;
	        }
	    }

	    private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
	    {
	        var canvas = e.Surface.Canvas;

	        canvas.Clear();
            
	        DrawTool.DrawImagesOnCanvas(
	            canvas, _viewModel.LeftBitmap, _viewModel.RightBitmap,
	            _viewModel.Settings.BorderThicknessProportion, _viewModel.Settings.AddBorder, _viewModel.Settings.BorderColor,
	            _viewModel.LeftCrop + _viewModel.OutsideCrop, _viewModel.InsideCrop + _viewModel.RightCrop, _viewModel.InsideCrop + _viewModel.LeftCrop, _viewModel.RightCrop + _viewModel.OutsideCrop,
	            _viewModel.TopCrop, _viewModel.BottomCrop,
                _viewModel.LeftRotation, _viewModel.RightRotation, 
	            _viewModel.VerticalAlignment,
	            _viewModel.LeftZoom, _viewModel.RightZoom,
	            _viewModel.LeftKeystone, _viewModel.RightKeystone);
        }

        private void ResetGuides()
        {
            var rollBounds = AbsoluteLayout.GetLayoutBounds(_horizontalLevelWhole);
            var pitchBounds = AbsoluteLayout.GetLayoutBounds(_pitchIndicator);
            var yawBounds = AbsoluteLayout.GetLayoutBounds(_yawIndicator);
            if (_viewModel == null || _viewModel.IsViewPortrait)
            {
                rollBounds.Y = pitchBounds.Y = yawBounds.Y = ROTATION_GUIDES_PORTRAIT_Y;

                AbsoluteLayout.SetLayoutFlags(_upperLine, AbsoluteLayoutFlags.YProportional | AbsoluteLayoutFlags.WidthProportional);
                AbsoluteLayout.SetLayoutBounds(_upperLine, _upperLineBoundsPortrait);
                AbsoluteLayout.SetLayoutFlags(_upperLinePanner, AbsoluteLayoutFlags.YProportional | AbsoluteLayoutFlags.WidthProportional);
                AbsoluteLayout.SetLayoutBounds(_upperLinePanner, _upperLineBoundsPortrait);

                AbsoluteLayout.SetLayoutFlags(_lowerLine, AbsoluteLayoutFlags.YProportional | AbsoluteLayoutFlags.WidthProportional);
                AbsoluteLayout.SetLayoutBounds(_lowerLine, _lowerLinesBoundsPortrait);
                AbsoluteLayout.SetLayoutFlags(_lowerLinePanner, AbsoluteLayoutFlags.YProportional | AbsoluteLayoutFlags.WidthProportional);
                AbsoluteLayout.SetLayoutBounds(_lowerLinePanner, _lowerLinesBoundsPortrait);
            }
            else
            {
                rollBounds.Y = pitchBounds.Y = yawBounds.Y = ROTATION_GUIDES_LANDSCAPE_Y;

                AbsoluteLayout.SetLayoutFlags(_upperLine, AbsoluteLayoutFlags.YProportional | AbsoluteLayoutFlags.WidthProportional);
                AbsoluteLayout.SetLayoutBounds(_upperLine, _upperLineBoundsLandscape);
                AbsoluteLayout.SetLayoutFlags(_upperLinePanner, AbsoluteLayoutFlags.YProportional | AbsoluteLayoutFlags.WidthProportional);
                AbsoluteLayout.SetLayoutBounds(_upperLinePanner, _upperLineBoundsLandscape);

                AbsoluteLayout.SetLayoutFlags(_lowerLine, AbsoluteLayoutFlags.YProportional | AbsoluteLayoutFlags.WidthProportional);
                AbsoluteLayout.SetLayoutBounds(_lowerLine, _lowerLineBoundsLandscape);
                AbsoluteLayout.SetLayoutFlags(_lowerLinePanner, AbsoluteLayoutFlags.YProportional | AbsoluteLayoutFlags.WidthProportional);
                AbsoluteLayout.SetLayoutBounds(_lowerLinePanner, _lowerLineBoundsLandscape);
            }
            rollBounds.X = _viewModel == null || _viewModel.CameraColumn == 0 ? 0.2 : 0.8;
            AbsoluteLayout.SetLayoutBounds(_horizontalLevelWhole, rollBounds);
            pitchBounds.X = _viewModel == null || _viewModel.CameraColumn == 0 ? 0.1 : 0.9;
            AbsoluteLayout.SetLayoutBounds(_pitchIndicator, pitchBounds);
            yawBounds.X = _viewModel == null || _viewModel.CameraColumn == 0 ? 0.375 : 0.625;
            AbsoluteLayout.SetLayoutBounds(_yawIndicator, yawBounds);

            AbsoluteLayout.SetLayoutFlags(_leftReticle, AbsoluteLayoutFlags.All);
            AbsoluteLayout.SetLayoutBounds(_leftReticle, _leftReticleBounds);
            AbsoluteLayout.SetLayoutFlags(_leftReticlePanner, AbsoluteLayoutFlags.All);
            AbsoluteLayout.SetLayoutBounds(_leftReticlePanner, _leftReticleBounds);

            AbsoluteLayout.SetLayoutFlags(_rightReticle, AbsoluteLayoutFlags.All);
            AbsoluteLayout.SetLayoutBounds(_rightReticle, _rightReticleBounds);
            AbsoluteLayout.SetLayoutFlags(_rightReticlePanner, AbsoluteLayoutFlags.All);
            AbsoluteLayout.SetLayoutBounds(_rightReticlePanner, _rightReticleBounds);
        }

        private void ReticlePanned(object sender, PanUpdatedEventArgs e)
	    {
	        if (e.StatusType == GestureStatus.Started)
	        {
	            var originalBounds = AbsoluteLayout.GetLayoutBounds(_leftReticlePanner);
	            _reticleLeftX = _leftReticle.X;
	            _reticleY = _leftReticle.Y;
	            _reticleRightX = _rightReticle.X;
	            _reticleWidth = originalBounds.Width;
            }
	        else if (e.StatusType == GestureStatus.Running)
	        {
	            if (AbsoluteLayout.GetLayoutFlags(_leftReticle) != AbsoluteLayoutFlags.SizeProportional)
	            {
	                AbsoluteLayout.SetLayoutFlags(_leftReticle, AbsoluteLayoutFlags.SizeProportional);
	                AbsoluteLayout.SetLayoutFlags(_rightReticle, AbsoluteLayoutFlags.SizeProportional);
	            }

                AbsoluteLayout.SetLayoutBounds(_leftReticle, new Rectangle(
	                _reticleLeftX + e.TotalX,
	                _reticleY + e.TotalY,
	                _reticleWidth,
	                _reticleWidth));

	            AbsoluteLayout.SetLayoutBounds(_rightReticle, new Rectangle(
	                _reticleRightX + e.TotalX,
	                _reticleY + e.TotalY,
	                _reticleWidth,
	                _reticleWidth));
            }
	        else if (e.StatusType == GestureStatus.Completed)
	        {
	            if (AbsoluteLayout.GetLayoutFlags(_leftReticlePanner) != AbsoluteLayoutFlags.SizeProportional)
	            {
	                AbsoluteLayout.SetLayoutFlags(_leftReticlePanner, AbsoluteLayoutFlags.SizeProportional);
	                AbsoluteLayout.SetLayoutFlags(_rightReticlePanner, AbsoluteLayoutFlags.SizeProportional);
	            }

	            var newLeftReticleBounds = AbsoluteLayout.GetLayoutBounds(_leftReticle);
	            AbsoluteLayout.SetLayoutBounds(_leftReticlePanner, new Rectangle(
	                newLeftReticleBounds.X,
	                newLeftReticleBounds.Y,
	                _reticleWidth,
	                _reticleWidth));

	            var newRightReticleBounds = AbsoluteLayout.GetLayoutBounds(_rightReticle);
	            AbsoluteLayout.SetLayoutBounds(_rightReticlePanner, new Rectangle(
	                newRightReticleBounds.X,
	                newRightReticleBounds.Y,
	                _reticleWidth,
	                _reticleWidth));
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