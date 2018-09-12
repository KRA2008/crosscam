using System;
using Android.Content;
using Android.Hardware;
using Android.Runtime;
using Android.Views;

namespace CustomRenderer.Droid
{
    public sealed class OrientationHelper : OrientationEventListener
    {
        private readonly IWindowManager _windowManager;
        private int _orientation;

        public OrientationHelper(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer) {}

        public OrientationHelper(Context context, IWindowManager windowManager) : base(context)
        {
            _windowManager = windowManager;
            _orientation = _windowManager.DefaultDisplay.Orientation;
        }

        public OrientationHelper(Context context, SensorDelay rate) : base(context, rate) {}

        public override void OnOrientationChanged(int orientation)
        {
            if (_windowManager.DefaultDisplay.Orientation != _orientation)
            {
                _orientation = _windowManager.DefaultDisplay.Orientation;
                OnOrientationChanged(new EventArgs());
            }
        }

        public event EventHandler OrientationChanged;

        private void OnOrientationChanged(EventArgs e)
        {
            var handler = OrientationChanged;
            handler?.Invoke(this, e);
        }
    }
}