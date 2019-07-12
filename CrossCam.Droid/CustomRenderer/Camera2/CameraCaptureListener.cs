using System;
using Android.Hardware.Camera2;

namespace CrossCam.Droid.CustomRenderer.Camera2
{
    public class CameraCaptureListener : CameraCaptureSession.CaptureCallback
    {
        public event EventHandler<CameraCaptureListenerEventArgs> CaptureComplete;
        public event EventHandler<CameraCaptureListenerEventArgs> CaptureProgressed;

        public override void OnCaptureCompleted(CameraCaptureSession session, CaptureRequest request,
            TotalCaptureResult result)
        {
            base.OnCaptureCompleted(session, request, result);

            CaptureComplete?.Invoke(this, new CameraCaptureListenerEventArgs
            {
                CaptureRequest = request,
                CaptureResult = result
            });
        }

        public override void OnCaptureProgressed(CameraCaptureSession session, CaptureRequest request, CaptureResult partialResult)
        {
            base.OnCaptureProgressed(session, request, partialResult);

            CaptureProgressed?.Invoke(this, new CameraCaptureListenerEventArgs
            {
                CaptureRequest = request,
                CaptureResult = partialResult
            });
        }

        public class CameraCaptureListenerEventArgs : EventArgs
        {
            public CaptureRequest CaptureRequest { get; set; }
            public CaptureResult CaptureResult { get; set; }
        }
    }
}
