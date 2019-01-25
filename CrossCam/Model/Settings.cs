using System.ComponentModel;
using CrossCam.Page;

namespace CrossCam.Model
{
    public class Settings : INotifyPropertyChanged
    {
        public bool HasOfferedTechniqueHelpBefore { get; set; }
        public bool HasShownDirectionsBefore { get; set; }

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

        private BorderColor _borderColor;
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

        private int _resolutionProportion;
        public int ResolutionProportion
        {
            get => _resolutionProportion;
            set
            {
                if (value > 0)
                {
                    _resolutionProportion = value;
                }
            }
        }

        private int _cropSpeed;
        public int CropSpeed
        {
            get => _cropSpeed;
            set
            {
                if (value > 0)
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
                if (value > 0)
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
                if (value > 0)
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
                if (value > 0)
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
                if (value > 0)
                {
                    _keystoneSpeed = value;
                }
            }
        }

        private int _borderThicknessProportion;
        public int BorderThicknessProportion
        {
            get => _borderThicknessProportion;
            set
            {
                if (value > 0)
                {
                    _borderThicknessProportion = value;
                }
            }
        }

        public bool IsAutomaticAlignmentOn { get; set; }

        private int _automaticAlignmentEpsilonLevel;
        public int AutomaticAlignmentEpsilonLevel
        {
            get => _automaticAlignmentEpsilonLevel;
            set
            {
                if (value > 0)
                {
                    _automaticAlignmentEpsilonLevel = value;
                }
            }
        }

        private int _automaticAlignmentIterations;
        public int AutomaticAlignmentIterations
        {
            get => _automaticAlignmentIterations;
            set
            {
                if (value > 0)
                {
                    _automaticAlignmentIterations = value;
                }
            }
        }

        private int _automaticAlignmentDownsizePercentage;
        public int AutomaticAlignmentDownsizePercentage
        {
            get => _automaticAlignmentDownsizePercentage;
            set
            {
                if (value > 0)
                {
                    _automaticAlignmentDownsizePercentage = value;
                }
            }
        }

        private int _automaticAlignmentEccThresholdPercentage;
        public int AutomaticAlignmentEccThresholdPercentage
        {
            get => _automaticAlignmentEccThresholdPercentage;
            set
            {
                if (value > 0)
                {
                    _automaticAlignmentEccThresholdPercentage = value;
                }
            }
        }

        public Settings()
        {
            HasOfferedTechniqueHelpBefore = false;
            HasShownDirectionsBefore = false;
            ResetToDefaults();
        }

        public void ResetToDefaults()
        {
            AreGuideLinesVisible = true;
            IsCaptureLeftFirst = true;
            SaveForCrossView = true;
            ShowRollGuide = true;

            ShowPitchGuide = false;
            ShowYawGuide = false;

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

            IsAutomaticAlignmentOn = true;
            AutomaticAlignmentDownsizePercentage = 25;
            AutomaticAlignmentEpsilonLevel = 4;
            AutomaticAlignmentIterations = 100;
            AutomaticAlignmentEccThresholdPercentage = 60;

            ResolutionProportion = 100;
            RotationSpeed = 10;
            ZoomSpeed = 20;
            AlignSpeed = 10;
            CropSpeed = 20;
            BorderThicknessProportion = 25;
            KeystoneSpeed = 50;
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}