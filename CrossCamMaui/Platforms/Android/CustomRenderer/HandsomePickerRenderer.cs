using Android.Content;
using Microsoft.Maui.Controls.Platform;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Compatibility.Platform.Android;
using Microsoft.Maui.Controls.Compatibility.Platform.Android.AppCompat;

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
                Control.SetTextColor(Microsoft.Maui.Graphics.Colors.White.ToAndroid());
                Control.SetBackgroundColor(Microsoft.Maui.Graphics.Colors.Black.ToAndroid());
            }
        }
    }
}