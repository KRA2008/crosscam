using System.ComponentModel;

namespace CrossCam.Model
{
    public class Settings : INotifyPropertyChanged
    {
        public bool AreGuideLinesVisible { get; set; }
        public bool IsGuideDonutVisible { get; set; }
        public bool SaveSidesSeparately { get; set; }
        public bool SaveLandscapeFullWidth { get; set; }

        public Settings()
        {
            AreGuideLinesVisible = true;
            IsGuideDonutVisible = false;
            SaveSidesSeparately = false;
            SaveLandscapeFullWidth = false;
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}