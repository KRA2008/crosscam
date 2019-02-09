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
        public bool SaveForAnaglyph { get; set; }
        public bool AddBorder { get; set; }
        public bool ShowRollGuide { get; set; }
        public bool ShowPitchGuide { get; set; }
        public bool ShowYawGuide { get; set; }

        public bool LeftyMode { get; set; }

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

        private int _borderWidthProportion;
        public int BorderWidthProportion
        {
            get => _borderWidthProportion;
            set
            {
                if (value > 0)
                {
                    _borderWidthProportion = value;
                }
            }
        }

        public bool IsAutomaticAlignmentOn { get; set; }

        private int _alignmentEpsilonLevel;
        public int AlignmentEpsilonLevel
        {
            get => _alignmentEpsilonLevel;
            set
            {
                if (value > 0)
                {
                    _alignmentEpsilonLevel = value;
                }
            }
        }

        private int _alignmentIterations;
        public int AlignmentIterations
        {
            get => _alignmentIterations;
            set
            {
                if (value > 0)
                {
                    _alignmentIterations = value;
                }
            }
        }

        private int _alignmentDownsizePercentage;
        public int AlignmentDownsizePercentage
        {
            get => _alignmentDownsizePercentage;
            set
            {
                if (value > 0)
                {
                    _alignmentDownsizePercentage = value;
                }
            }
        }

        private int _alignmentEccThresholdPercentage;
        public int AlignmentEccThresholdPercentage
        {
            get => _alignmentEccThresholdPercentage;
            set
            {
                if (value > 0)
                {
                    _alignmentEccThresholdPercentage = value;
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
            AddBorder = true;

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
            SaveForAnaglyph = false;

            LeftyMode = false;

            BorderColor = BorderColor.Black;

            IsAutomaticAlignmentOn = true;
            AlignmentDownsizePercentage = 35;
            AlignmentEpsilonLevel = 3;
            AlignmentIterations = 50;
            AlignmentEccThresholdPercentage = 60;

            ResolutionProportion = 100;
            BorderWidthProportion = 10;
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}