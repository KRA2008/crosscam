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

	    public static readonly BindableProperty IsFullScreenPreviewProperty = BindableProperty.Create(nameof(IsFullScreenPreview),
	        typeof(bool), typeof(CameraModule), false, BindingMode.TwoWay);

	    public static readonly BindableProperty IsTapToFocusEnabledProperty = BindableProperty.Create(nameof(IsTapToFocusEnabled),
	        typeof(bool), typeof(CameraModule), false, BindingMode.TwoWay);

	    public static readonly BindableProperty SwitchToContinuousFocusTriggerProperty = BindableProperty.Create(nameof(SwitchToContinuousFocusTrigger),
	        typeof(bool), typeof(CameraModule), false, BindingMode.TwoWay);

	    public static readonly BindableProperty ErrorMessageProperty = BindableProperty.Create(nameof(ErrorMessage),
	        typeof(string), typeof(CameraModule), null, BindingMode.TwoWay);

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

	    public bool IsFullScreenPreview
	    {
	        get => (bool)GetValue(IsFullScreenPreviewProperty);
	        set => SetValue(IsFullScreenPreviewProperty, value);
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
    }
}