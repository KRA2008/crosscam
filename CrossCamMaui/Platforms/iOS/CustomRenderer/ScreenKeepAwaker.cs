using CrossCam.Wrappers;
using UIKit;

namespace CrossCam.Platforms.iOS.CustomRenderer
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