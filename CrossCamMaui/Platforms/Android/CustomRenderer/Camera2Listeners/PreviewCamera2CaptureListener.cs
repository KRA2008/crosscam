using Android.Hardware.Camera2;

namespace CrossCam.Platforms.Android.CustomRenderer.Camera2Listeners
{
    public class PreviewCamera2CaptureListener : CrossCamCamera2CaptureListener
    {
        public override void OnCaptureCompleted(CameraCaptureSession session, CaptureRequest request, TotalCaptureResult result)
        {
            base.OnCaptureCompleted(session, request, result);
        }

        public override void OnCaptureProgressed(CameraCaptureSession session, CaptureRequest request, CaptureResult partialResult)
        {
            base.OnCaptureProgressed(session, request, partialResult);
        }
    }
}