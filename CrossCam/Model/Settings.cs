using System.ComponentModel;

namespace CrossCam.Model
{
    public class Settings : INotifyPropertyChanged
    {
        public bool AreGuideLinesVisible { get; set; }
        public bool ShowGuideLinesWithFirstCapture { get; set; }
        public bool IsGuideDonutVisible { get; set; }
        public bool ShowGuideDonutWithFirstCapture { get; set; }
        public bool IsGuideDonutBothDonuts { get; set; }
        public bool SaveSidesSeparately { get; set; }
        public bool FillScreenPreview { get; set; }
        public bool ClipLandscapeToFilledScreenPreview { get; set; }
        public bool IsCaptureLeftFirst { get; set; }
        public bool IsTapToFocusEnabled { get; set; }
        public bool SaveRedundantFirstSide { get; set; }

        public Settings()
        {
            ResetToDefaults();
        }

        public void ResetToDefaults()
        {
            AreGuideLinesVisible = true;
            IsCaptureLeftFirst = true;

            ShowGuideLinesWithFirstCapture = false;
            IsGuideDonutVisible = false;
            ShowGuideDonutWithFirstCapture = false;
            IsGuideDonutBothDonuts = false;
            SaveSidesSeparately = false;
            FillScreenPreview = false;
            ClipLandscapeToFilledScreenPreview = false;
            IsTapToFocusEnabled = false;
            SaveRedundantFirstSide = false;
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}