using Android.Views;
using CrossCam.Wrappers;

namespace CrossCam.Platforms.Android.CustomRenderer
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