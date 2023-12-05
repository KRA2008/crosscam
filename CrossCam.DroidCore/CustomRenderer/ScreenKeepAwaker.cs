using Android.Views;
using CrossCam.Droid.CustomRenderer;
using CrossCam.Wrappers;
using Microsoft.Maui.Controls;
using Microsoft.Maui;

[assembly: Dependency(typeof(ScreenKeepAwaker))]
namespace CrossCam.Droid.CustomRenderer
{
    public class ScreenKeepAwaker : IScreenKeepAwaker
    {
        public void KeepScreenAwake()
        {
            MainActivity.Instance.Window?.AddFlags(WindowManagerFlags.KeepScreenOn);
        }

        public void LetScreenSleep()
        {
            MainActivity.Instance.Window?.ClearFlags(WindowManagerFlags.KeepScreenOn);
        }
    }
}