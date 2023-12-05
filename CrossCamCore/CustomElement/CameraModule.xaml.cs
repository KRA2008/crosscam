using System.Collections.ObjectModel;
using CrossCam.Model;
using CrossCam.ViewModel;
using PointF = System.Drawing.PointF;

namespace CrossCam.CustomElement
{
	public sealed partial class CameraModule
	{
		public CameraModule()
		{
			InitializeComponent();
	    }

        public EventHandler<PointF> SingleTapped;
        public void OnSingleTapped(PointF point)
        {
            SingleTapped?.Invoke(this, point);
        }

        public EventHandler DoubleTapped;
        public void OnDoubleTapped()
        {
            DoubleTapped?.Invoke(this, EventArgs.Empty);
        }

        public static readonly BindableProperty CapturedImageProperty = BindableProperty.Create(nameof(CapturedImage),
	        typeof(IncomingFrame), typeof(CameraModule), null, BindingMode.OneWayToSource);

        public static readonly BindableProperty PreviewImageProperty = BindableProperty.Create(nameof(PreviewImage),
            typeof(IncomingFrame), typeof(CameraModule), null, BindingMode.OneWayToSource);

        public static readonly BindableProperty CaptureTriggerProperty = BindableProperty.Create(nameof(CaptureTrigger),
            typeof(bool), typeof(CameraModule), false);

	    public static readonly BindableProperty CaptureSuccessProperty = BindableProperty.Create(nameof(CaptureSuccess),
	        typeof(bool), typeof(CameraModule), false, BindingMode.OneWayToSource);

        public static readonly BindableProperty IsTapToFocusEnabledProperty = BindableProperty.Create(nameof(IsTapToFocusEnabled),
	        typeof(bool), typeof(CameraModule), false);

        public static readonly BindableProperty IsLockToFirstEnabledProperty = BindableProperty.Create(nameof(IsLockToFirstEnabled),
            typeof(bool), typeof(CameraModule), true);

        public static readonly BindableProperty SwitchToContinuousFocusTriggerProperty = BindableProperty.Create(nameof(SwitchToContinuousFocusTrigger),
	        typeof(bool), typeof(CameraModule), false);

        public static readonly BindableProperty WasSwipedTriggerProperty = BindableProperty.Create(nameof(WasSwipedTrigger),
            typeof(bool), typeof(CameraModule), false, BindingMode.OneWayToSource);

        public static readonly BindableProperty ErrorProperty = BindableProperty.Create(nameof(Error),
	        typeof(Exception), typeof(CameraModule), null, BindingMode.OneWayToSource);

	    public static readonly BindableProperty IsNothingCapturedProperty = BindableProperty.Create(nameof(IsNothingCaptured),
	        typeof(bool), typeof(CameraModule), true);

        public static readonly BindableProperty AreBothSidesCapturedProperty = BindableProperty.Create(nameof(AreBothSidesCaptured),
            typeof(bool), typeof(CameraModule), true);

        public static readonly BindableProperty PreviewBottomYProperty = BindableProperty.Create(nameof(PreviewBottomY),
            typeof(double), typeof(CameraModule), 0d, BindingMode.OneWayToSource);

        public static readonly BindableProperty PreviewAspectRatioProperty = BindableProperty.Create(nameof(PreviewAspectRatio),
            typeof(double), typeof(CameraModule), 0d, BindingMode.OneWayToSource);

        public static readonly BindableProperty IsFocusCircleLockedProperty = BindableProperty.Create(nameof(IsFocusCircleLocked),
            typeof(bool), typeof(CameraModule), false, BindingMode.OneWayToSource);

        public static readonly BindableProperty PairOperatorProperty = BindableProperty.Create(nameof(PairOperator),
            typeof(PairOperator), typeof(CameraModule));

        public static readonly BindableProperty AvailableCamerasProperty = BindableProperty.Create(nameof(AvailableCameras),
            typeof(ObservableCollection<AvailableCamera>), typeof(CameraModule), defaultBindingMode:BindingMode.TwoWay);

        public static readonly BindableProperty ChosenCameraProperty = BindableProperty.Create(nameof(ChosenCamera),
            typeof(AvailableCamera), typeof(CameraModule), defaultBindingMode:BindingMode.TwoWay);
        
        public static readonly BindableProperty PreviewModeProperty = BindableProperty.Create(nameof(PreviewMode),
            typeof(DrawMode), typeof(CameraModule), DrawMode.Cross);

        public static readonly BindableProperty RestartPreviewTriggerProperty = BindableProperty.Create(nameof(RestartPreviewTrigger),
            typeof(bool), typeof(CameraModule), false);

        public static readonly BindableProperty StopPreviewTriggerProperty = BindableProperty.Create(nameof(StopPreviewTrigger),
            typeof(bool), typeof(CameraModule), false);

        public IncomingFrame CapturedImage
        {
            get => (IncomingFrame)GetValue(CapturedImageProperty);
            set => SetValue(CapturedImageProperty, value);
        }

        public IncomingFrame PreviewImage
        {
            get => (IncomingFrame)GetValue(PreviewImageProperty);
            set => SetValue(PreviewImageProperty, value);
        }

        public bool CaptureTrigger
        {
            get => (bool)GetValue(CaptureTriggerProperty);
            set => SetValue(CaptureTriggerProperty, value);
        }

	    public bool CaptureSuccess
	    {
	        get => (bool)GetValue(CaptureSuccessProperty);
	        set => SetValue(CaptureSuccessProperty, value);
	    }

        public bool IsTapToFocusEnabled
	    {
	        get => (bool)GetValue(IsTapToFocusEnabledProperty);
	        set => SetValue(IsTapToFocusEnabledProperty, value);
	    }

        public bool IsLockToFirstEnabled
        {
            get => (bool)GetValue(IsLockToFirstEnabledProperty);
            set => SetValue(IsLockToFirstEnabledProperty, value);
        }

        public bool SwitchToContinuousFocusTrigger
        {
	        get => (bool)GetValue(SwitchToContinuousFocusTriggerProperty);
	        set => SetValue(SwitchToContinuousFocusTriggerProperty, value);
	    }
        
        public bool WasSwipedTrigger
        {
            get => (bool)GetValue(WasSwipedTriggerProperty);
            set => SetValue(WasSwipedTriggerProperty, value);
        }

        public Exception Error
	    {
	        get => (Exception)GetValue(ErrorProperty);
	        set => SetValue(ErrorProperty, value);
	    }

	    public bool IsNothingCaptured
	    {
	        get => (bool)GetValue(IsNothingCapturedProperty);
	        set => SetValue(IsNothingCapturedProperty, value);
        }

        public bool AreBothSidesCaptured
        {
            get => (bool)GetValue(AreBothSidesCapturedProperty);
            set => SetValue(AreBothSidesCapturedProperty, value);
        }

        public double PreviewBottomY
        {
            get => (double)GetValue(PreviewBottomYProperty);
            set => SetValue(PreviewBottomYProperty, value);
        }

        public double PreviewAspectRatio
        {
            get => (double)GetValue(PreviewAspectRatioProperty);
            set => SetValue(PreviewAspectRatioProperty, value);
        }

        public bool IsFocusCircleLocked
        {
            get => (bool)GetValue(IsFocusCircleLockedProperty);
            set => SetValue(IsFocusCircleLockedProperty, value);
        }

        public PairOperator PairOperator
        {
            get => (PairOperator)GetValue(PairOperatorProperty);
            set => SetValue(PairOperatorProperty, value);
        }

        public ObservableCollection<AvailableCamera> AvailableCameras
        {
            get => (ObservableCollection<AvailableCamera>)GetValue(AvailableCamerasProperty);
            set => SetValue(AvailableCamerasProperty, value);
        }

        public AvailableCamera ChosenCamera
        {
            get => (AvailableCamera)GetValue(ChosenCameraProperty);
            set => SetValue(ChosenCameraProperty, value);
        }

        public DrawMode PreviewMode
        {
            get => (DrawMode)GetValue(PreviewModeProperty);
            set => SetValue(PreviewModeProperty, value);
        }

        public bool RestartPreviewTrigger
        {
            get => (bool)GetValue(RestartPreviewTriggerProperty);
            set => SetValue(RestartPreviewTriggerProperty, value);
        }

        public bool StopPreviewTrigger
        {
            get => (bool)GetValue(StopPreviewTriggerProperty);
            set => SetValue(StopPreviewTriggerProperty, value);
        }
    }
}