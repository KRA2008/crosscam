using System.ComponentModel;
using CrossCam.Page;

namespace CrossCam.Model
{
    public class Settings : INotifyPropertyChanged
    {
        public bool ShowGuideLinesWithFirstCapture { get; set; }
        public bool ShowGuideDonutWithFirstCapture { get; set; }
        public bool IsGuideDonutBothDonuts { get; set; }
        public bool IsCaptureLeftFirst { get; set; }
        public bool IsTapToFocusEnabled { get; set; }
        public bool SaveRedundantFirstSide { get; set; }
        public bool SaveForParallel { get; set; }
        public bool SaveForCrossView { get; set; }
        public bool AddBorder { get; set; }
        public bool ShowRollGuide { get; set; }
        public bool ShowPitchGuide { get; set; }
        public bool ShowYawGuide { get; set; }

        public BorderColor BorderColor
        {
            get => _borderColor;
            set
            {
                var intValue = (int) value;
                if (intValue < 0) return;
                _borderColor = value;
            }
        }

        private bool _areGuideLinesVisible;
        public bool AreGuideLinesVisible
        {
            get => _areGuideLinesVisible;
            set
            {
                _areGuideLinesVisible = value;
                if (!value)
                {
                    ShowGuideLinesWithFirstCapture = false;
                }
            }
        }

        private bool _isGuideDonutVisible;
        public bool IsGuideDonutVisible
        {
            get => _isGuideDonutVisible;
            set
            {
                _isGuideDonutVisible = value;
                if (!value)
                {
                    ShowGuideDonutWithFirstCapture = false;
                    IsGuideDonutBothDonuts = false;
                }
            }
        }

        private bool _saveSidesSeparately;
        public bool SaveSidesSeparately
        {
            get => _saveSidesSeparately;
            set
            {
                _saveSidesSeparately = value;
                if (value)
                {
                    SaveRedundantFirstSide = false;
                }
            }
        }

        private int _cropSpeed;
        public int CropSpeed
        {
            get => _cropSpeed;
            set
            {
                if (value >= 0)
                {
                    _cropSpeed = value;
                }
            }
        }

        private int _zoomSpeed;
        public int ZoomSpeed
        {
            get => _zoomSpeed;
            set
            {
                if (value >= 0)
                {
                    _zoomSpeed = value;
                }
            }
        }

        private int _alignSpeed;
        public int AlignSpeed
        {
            get => _alignSpeed;
            set
            {
                if (value >= 0)
                {
                    _alignSpeed = value;
                }
            }
        }

        private int _rotationSpeed;
        public int RotationSpeed
        {
            get => _rotationSpeed;
            set
            {
                if (value >= 0)
                {
                    _rotationSpeed = value;
                }
            }
        }

        private int _keystoneSpeed;
        public int KeystoneSpeed
        {
            get => _keystoneSpeed;
            set
            {
                if (value >= 0)
                {
                    _keystoneSpeed = value;
                }
            }
        }

        private int _borderThickness;
        private BorderColor _borderColor;

        public int BorderThickness
        {
            get => _borderThickness;
            set
            {
                if (value >= 0)
                {
                    _borderThickness = value;
                }
            }
        }

        public Settings()
        {
            ResetToDefaults();
        }

        public void ResetToDefaults()
        {
            AreGuideLinesVisible = true;
            IsCaptureLeftFirst = true;
            SaveForCrossView = true;
            ShowRollGuide = true;
            ShowPitchGuide = true;
            ShowYawGuide = true;

            ShowGuideLinesWithFirstCapture = false;
            IsGuideDonutVisible = false;
            ShowGuideDonutWithFirstCapture = false;
            IsGuideDonutBothDonuts = false;
            SaveSidesSeparately = false;
            IsTapToFocusEnabled = false;
            SaveRedundantFirstSide = false;
            SaveForParallel = false;
            AddBorder = false;

            BorderColor = BorderColor.Black;

            RotationSpeed = 10;
            ZoomSpeed = 20;
            AlignSpeed = 10;
            CropSpeed = 20;
            BorderThickness = 60;
            KeystoneSpeed = 50;
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}