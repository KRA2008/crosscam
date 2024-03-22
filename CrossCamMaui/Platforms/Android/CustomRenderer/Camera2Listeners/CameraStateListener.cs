using Android.Hardware.Camera2;

namespace CrossCam.Platforms.Android.CustomRenderer.Camera2Listeners
{
    public class CameraStateListener : CameraDevice.StateCallback
    {
        private readonly CameraModuleRenderer _cameraModuleRenderer;

        public CameraStateListener(CameraModuleRenderer cameraModuleRenderer)
        {
            _cameraModuleRenderer = cameraModuleRenderer;
        }

        public override void OnClosed(CameraDevice camera)
        {
        }

        public override void OnOpened(CameraDevice camera)
        {
            _cameraModuleRenderer.CreateCamera2PreviewSession(camera);
        }

        public override void OnDisconnected(CameraDevice camera)
        {
            _cameraModuleRenderer.Camera2Disconnected(camera);
        }

        public override void OnError(CameraDevice camera, CameraError error)
        {
            _cameraModuleRenderer.Camera2Errored(camera, error);
        }
    }
}
