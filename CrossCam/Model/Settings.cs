using System.ComponentModel;

namespace CrossCam.Model
{
    public class Settings : INotifyPropertyChanged
    {
        public bool AreGuideLinesVisible { get; set; }
        public bool ShowGuideLinesWithFirstCapture { get; set; }
        public bool IsGuideDonutVisible { get; set; }
        public bool ShowGuideDonutWithFirstCapture { get; set; }
        public bool SaveSidesSeparately { get; set; }
        public bool FillScreenPreview { get; set; }
        public bool ClipLandscapeToFilledScreenPreview { get; set; }
        public bool IsCaptureLeftFirst { get; set; }
        public bool IsTapToFocusEnabled { get; set; }

        public Settings()
        {
            AreGuideLinesVisible = true;
            IsCaptureLeftFirst = true;
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}