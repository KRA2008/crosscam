using Xamarin.Forms;

namespace CustomRenderer
{
	public sealed partial class CameraModule
	{
		public CameraModule ()
		{
			InitializeComponent ();
	    }

	    public static readonly BindableProperty CapturedImageProperty = BindableProperty.Create(nameof(CapturedImage),
	        typeof(byte[]), typeof(CameraModule), defaultBindingMode: BindingMode.TwoWay);

        public byte[] CapturedImage
        {
            get => (byte[])GetValue(CapturedImageProperty);
            set => SetValue(CapturedImageProperty, value);
        }
    }
}