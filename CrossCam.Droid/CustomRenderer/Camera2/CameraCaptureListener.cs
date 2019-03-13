using System;
using Android.Hardware.Camera2;

namespace CrossCam.Droid.CustomRenderer.Camera2
{
    public class CameraCaptureListener : CameraCaptureSession.CaptureCallback
    {
        public event EventHandler PhotoComplete;

        public override void OnCaptureCompleted(CameraCaptureSession session, CaptureRequest request,
            TotalCaptureResult result)
        {
            PhotoComplete?.Invoke(this, EventArgs.Empty);
        }
    }
}
