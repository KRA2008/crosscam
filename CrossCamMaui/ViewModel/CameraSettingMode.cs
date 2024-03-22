using PropertyChanged;

namespace CrossCam.ViewModel
{
    public enum CameraSettingMode
    {
        Menu,
        ISO,
        Exposure,
        FrameDuration,
        WhiteBalance,
        Camera,
        Flash
    }

    [AddINotifyPropertyChangedInterface]
    public class AvailableCamera
    {
        public bool IsFront { get; set; }
        public string CameraId { get; set; }
        private string _displayName;
        public string DisplayName
        {
            get => _displayName ??  (IsFront ? "Front" : "Back") + " (" + CameraId + ")";
            set => _displayName = value;
        }
    }
}