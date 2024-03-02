namespace CrossCam.Wrappers
{
    public interface IDeviceDisplayWrapper
    {
        void HoldScreenOn();
        void DoNotHoldScreenOn();
        double GetDisplayDensity();
        double GetDisplayWidth();
        double GetDisplayHeight();
        event EventHandler<DisplayInfoChangedEventArgs> DisplayInfoChanged;
    }

    public class DeviceDisplayWrapper : IDeviceDisplayWrapper
    {
        public DeviceDisplayWrapper()
        {
#if __IOS__
            MainThread.BeginInvokeOnMainThread(() =>
            {
#endif
                DeviceDisplay.MainDisplayInfoChanged += OnDisplayInfoChanged;
#if __IOS__
            });
#endif
        }

        private void OnDisplayInfoChanged(object sender, DisplayInfoChangedEventArgs e)
        {
            DisplayInfoChanged?.Invoke(sender, e);
        }


        public DisplayRotation GetRotation()
        {
            var rotation = DisplayRotation.Unknown;
#if __IOS__
            MainThread.BeginInvokeOnMainThread(() =>
            {
#endif
                rotation = DeviceDisplay.MainDisplayInfo.Rotation;
#if __IOS__
            });
#endif
            return rotation;
        }

        public void HoldScreenOn()
        {
#if __IOS__
            MainThread.BeginInvokeOnMainThread(() =>
            {
#endif
                DeviceDisplay.KeepScreenOn = true;
#if __IOS__
            });
#endif
        }

        public void DoNotHoldScreenOn()
        {
#if __IOS__
            MainThread.BeginInvokeOnMainThread(() =>
            {
#endif
                DeviceDisplay.KeepScreenOn = false;
#if __IOS__
            });
#endif
        }

        public double GetDisplayDensity()
        {
            double density = 0;
#if __IOS__
            MainThread.BeginInvokeOnMainThread(() =>
            {
#endif
                density = DeviceDisplay.MainDisplayInfo.Density;
#if __IOS__
            });
#endif
            return density;
        }

        public double GetDisplayWidth()
        {
            double width = 0;
#if __IOS__
            MainThread.BeginInvokeOnMainThread(() =>
            {
#endif
                width = DeviceDisplay.MainDisplayInfo.Width;
#if __IOS__
            });
#endif
            return width;
        }

        public double GetDisplayHeight()
        {
            double height = 0;
#if __IOS__
            MainThread.BeginInvokeOnMainThread(() =>
            {
#endif
                height = DeviceDisplay.MainDisplayInfo.Height;
#if __IOS__
            });
#endif
            return height;
        }

        public event EventHandler<DisplayInfoChangedEventArgs> DisplayInfoChanged;
    }
}
