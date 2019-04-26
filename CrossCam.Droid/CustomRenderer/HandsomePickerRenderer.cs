using Android.Content;
using CrossCam.Droid.CustomRenderer;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;
using PickerRenderer = Xamarin.Forms.Platform.Android.AppCompat.PickerRenderer;

[assembly: ExportRenderer(typeof(Picker), typeof(HandsomePickerRenderer))]
namespace CrossCam.Droid.CustomRenderer
{
    public class HandsomePickerRenderer : PickerRenderer
    {
        public HandsomePickerRenderer(Context context) : base(context) {}

        protected override void OnElementChanged(ElementChangedEventArgs<Picker> e)
        {
            base.OnElementChanged(e);

            if (Control != null && Element != null)
            {
                Control.SetTextColor(Color.White.ToAndroid());
                Control.SetBackgroundColor(Color.Black.ToAndroid());
            }
        }
    }
}