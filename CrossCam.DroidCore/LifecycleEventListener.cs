using System;
using Android.Content;
using Android.Views;

namespace CrossCam.Droid
{
    public sealed class LifecycleEventListener : OrientationEventListener
    {
        private readonly IWindowManager _windowManager;
        private int _orientation;

        public LifecycleEventListener(Context context, IWindowManager windowManager) : base(context)
        {
            _windowManager = windowManager;
            _orientation = _windowManager.DefaultDisplay.Orientation;
        }

        public override void OnOrientationChanged(int orientation)
        {
            if (_windowManager.DefaultDisplay.Orientation != _orientation)
            {
                _orientation = _windowManager.DefaultDisplay.Orientation;
                var handler = OrientationChanged;
                handler?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler OrientationChanged;

        public void OnAppMaximized()
        {
            var handler = AppMaximized;
            handler?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler AppMaximized;

        public void OnAppMinimized()
        {
            var handler = AppMinimized;
            handler?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler AppMinimized;
    }
}