using System.ComponentModel;
using CrossCam.ViewModel;
using SkiaSharp.Views.Forms;
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

        public CameraPage()
		{
            InitializeComponent();
            ResetGuides();
		    NavigationPage.SetHasNavigationBar(this, false);
        }

	    protected override void OnBindingContextChanged()
	    {
	        base.OnBindingContextChanged();
	        if (BindingContext != null)
	        {
	            _viewModel = (CameraViewModel) BindingContext;
	            _viewModel.PropertyChanged += ViewModelPropertyChanged;
	        }
	    }

	    private void ViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
	    {
	        switch (e.PropertyName)
	        {
	            case nameof(CameraViewModel.IsViewPortrait):
	                ResetGuides();
	                break;
	            case nameof(CameraViewModel.LeftBitmap):
                case nameof(CameraViewModel.RightBitmap):
                case nameof(CameraViewModel.LeftLeftCrop):
	            case nameof(CameraViewModel.LeftRightCrop):
	            case nameof(CameraViewModel.RightLeftCrop):
	            case nameof(CameraViewModel.RightRightCrop):
	            case nameof(CameraViewModel.LeftTopCrop):
	            case nameof(CameraViewModel.LeftBottomCrop):
	            case nameof(CameraViewModel.RightTopCrop):
	            case nameof(CameraViewModel.RightBottomCrop):
                case nameof(CameraViewModel.Settings):
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
	            _viewModel.Settings.BorderThickness, _viewModel.Settings.AddBorder,
	            _viewModel.LeftLeftCrop, _viewModel.LeftRightCrop, _viewModel.RightLeftCrop, _viewModel.RightRightCrop,
                _viewModel.LeftTopCrop, _viewModel.LeftBottomCrop, _viewModel.RightTopCrop, _viewModel.RightBottomCrop,
	            _viewModel.LeftRotation, _viewModel.RightRotation, _viewModel.VerticalAlignment,
	            _viewModel.LeftZoom, _viewModel.RightZoom,
	            _viewModel.LeftKeystone, _viewModel.RightKeystone);
        }

        private void ResetGuides()
        {
            if (_viewModel == null || _viewModel.IsViewPortrait)
            {
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
                AbsoluteLayout.SetLayoutFlags(_upperLine, AbsoluteLayoutFlags.YProportional | AbsoluteLayoutFlags.WidthProportional);
                AbsoluteLayout.SetLayoutBounds(_upperLine, _upperLineBoundsLandscape);
                AbsoluteLayout.SetLayoutFlags(_upperLinePanner, AbsoluteLayoutFlags.YProportional | AbsoluteLayoutFlags.WidthProportional);
                AbsoluteLayout.SetLayoutBounds(_upperLinePanner, _upperLineBoundsLandscape);

                AbsoluteLayout.SetLayoutFlags(_lowerLine, AbsoluteLayoutFlags.YProportional | AbsoluteLayoutFlags.WidthProportional);
                AbsoluteLayout.SetLayoutBounds(_lowerLine, _lowerLineBoundsLandscape);
                AbsoluteLayout.SetLayoutFlags(_lowerLinePanner, AbsoluteLayoutFlags.YProportional | AbsoluteLayoutFlags.WidthProportional);
                AbsoluteLayout.SetLayoutBounds(_lowerLinePanner, _lowerLineBoundsLandscape);
            }

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