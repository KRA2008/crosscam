using Xamarin.Forms;

namespace CrossCam.CustomElement
{
	public sealed partial class CameraModule
	{
		public CameraModule ()
		{
			InitializeComponent ();
	    }
        //TODO: what binding modes are needed?
	    public static readonly BindableProperty CapturedImageProperty = BindableProperty.Create(nameof(CapturedImage),
	        typeof(byte[]), typeof(CameraModule), defaultBindingMode: BindingMode.TwoWay);

        public static readonly BindableProperty CaptureTriggerProperty = BindableProperty.Create(nameof(CaptureTrigger),
            typeof(bool), typeof(CameraModule), false, BindingMode.TwoWay);

	    public static readonly BindableProperty CaptureSuccessProperty = BindableProperty.Create(nameof(CaptureSuccess),
	        typeof(bool), typeof(CameraModule), false, BindingMode.TwoWay);

	    public static readonly BindableProperty IsPortraitProperty = BindableProperty.Create(nameof(IsPortrait),
	        typeof(bool), typeof(CameraModule), false, BindingMode.TwoWay);

	    public static readonly BindableProperty IsViewInvertedProperty = BindableProperty.Create(nameof(IsViewInverted),
	        typeof(bool), typeof(CameraModule), false, BindingMode.TwoWay);

        public static readonly BindableProperty IsTapToFocusEnabledProperty = BindableProperty.Create(nameof(IsTapToFocusEnabled),
	        typeof(bool), typeof(CameraModule), false, BindingMode.TwoWay);

	    public static readonly BindableProperty SwitchToContinuousFocusTriggerProperty = BindableProperty.Create(nameof(SwitchToContinuousFocusTrigger),
	        typeof(bool), typeof(CameraModule), false, BindingMode.TwoWay);

        public static readonly BindableProperty ErrorMessageProperty = BindableProperty.Create(nameof(ErrorMessage),
	        typeof(string), typeof(CameraModule), null, BindingMode.TwoWay);

	    public static readonly BindableProperty IsNothingCapturedProperty = BindableProperty.Create(nameof(IsNothingCaptured),
	        typeof(bool), typeof(CameraModule), false, BindingMode.TwoWay);

	    public static readonly BindableProperty PreviewBottomYProperty = BindableProperty.Create(nameof(PreviewBottomY),
	        typeof(double), typeof(CameraModule), 0d, BindingMode.TwoWay);

        public byte[] CapturedImage
        {
            get => (byte[])GetValue(CapturedImageProperty);
            set => SetValue(CapturedImageProperty, value);
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

	    public bool IsPortrait
	    {
	        get => (bool)GetValue(IsPortraitProperty);
	        set => SetValue(IsPortraitProperty, value);
	    }

	    public bool IsViewInverted
        {
	        get => (bool)GetValue(IsViewInvertedProperty);
	        set => SetValue(IsViewInvertedProperty, value);
	    }

        public bool IsTapToFocusEnabled
	    {
	        get => (bool)GetValue(IsTapToFocusEnabledProperty);
	        set => SetValue(IsTapToFocusEnabledProperty, value);
	    }

	    public bool SwitchToContinuousFocusTrigger
        {
	        get => (bool)GetValue(SwitchToContinuousFocusTriggerProperty);
	        set => SetValue(SwitchToContinuousFocusTriggerProperty, value);
	    }

        public string ErrorMessage
	    {
	        get => (string)GetValue(ErrorMessageProperty);
	        set => SetValue(ErrorMessageProperty, value);
	    }

	    public bool IsNothingCaptured
	    {
	        get => (bool)GetValue(IsNothingCapturedProperty);
	        set => SetValue(IsNothingCapturedProperty, value);
	    }

	    public double PreviewBottomY
	    {
	        get => (double) GetValue(PreviewBottomYProperty);
	        set => SetValue(PreviewBottomYProperty, value);
        }
    }
}