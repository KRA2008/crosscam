using CrossCam.iOS.CustomRenderer;
using CrossCam.Wrappers;
using UIKit;
using Microsoft.Maui.Controls;
using Microsoft.Maui;

namespace CrossCam.iOS.CustomRenderer
{
    public class ScreenKeepAwaker : IScreenKeepAwaker
    {
        public void KeepScreenAwake()
        {
            UIApplication.SharedApplication.IdleTimerDisabled = true;
        }

        public void LetScreenSleep()
        {
            UIApplication.SharedApplication.IdleTimerDisabled = false;
        }
    }
}