using CrossCam.CustomElement;
using CrossCam.iOS.CustomRenderer;
using Xamarin.Forms;

[assembly: Dependency(typeof(NotchHeightProvider))]
namespace CrossCam.iOS.CustomRenderer
{
    public class NotchHeightProvider : INotchHeightProvider
    { 
        // inset on the bottom means it lacks a physical home button, which on iPhone means it also has a notch on top
        public int GetNotchHeight()
        {
            return HasInsets() ? 20 : 0;
        }

        public int GetHomeThingHeight()
        {
            return HasInsets() ? 10 : 0;
        }

        private static bool HasInsets()
        {
            var insets = UIKit.UIApplication.SharedApplication.KeyWindow.SafeAreaInsets;
            return Device.Idiom == TargetIdiom.Phone && insets.Bottom > 0;
        }
    }
}