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
    }
}