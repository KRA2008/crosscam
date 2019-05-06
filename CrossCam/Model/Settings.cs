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
        public bool ShowRollGuide { get; set; }

        public bool IsCaptureLeftFirst { get; set; }

        public bool IsForceCamera1Enabled { get; set; }
        public bool IsLockToFirstEnabled { get; set; }
        public bool IsTapToFocusEnabled { get; set; }

        public bool SaveRedundantFirstSide { get; set; }

        private bool _saveForParallel;
        public bool SaveForParallel
        {
            get => _saveForParallel;
            set
            {
                if (value)
                {
                    RedCyanAnaglyphMode = false;
                }
                _saveForParallel = value;
            }
        }

        private bool _saveForCrossView;
        public bool SaveForCrossView
        {
            get => _saveForCrossView;
            set
            {
                if (value)
                {
                    RedCyanAnaglyphMode = false;
                }
                _saveForCrossView = value;
            }
        }

        private bool _redCyanAnaglyphMode;
        public bool RedCyanAnaglyphMode
        {
            get => _redCyanAnaglyphMode;
            set
            {
                if (value)
                {
                    SaveForParallel = false;
                    SaveForCrossView = false;
                }
                _redCyanAnaglyphMode = value;
            }
        }

        public bool AddBorder { get; set; }

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

            AddBorder = false;

            ShowGuideLinesWithFirstCapture = false;
            IsGuideDonutVisible = false;
            ShowGuideDonutWithFirstCapture = false;
            SaveSidesSeparately = false;
            SaveRedundantFirstSide = false;
            SaveForParallel = false;

            RedCyanAnaglyphMode = false;

            IsForceCamera1Enabled = false;
            IsTapToFocusEnabled = false;
            IsLockToFirstEnabled = true;

            LeftyMode = false;

            BorderColor = BorderColor.Black;

            IsAutomaticAlignmentOn = true;
            AlignmentDownsizePercentage = 35;
            AlignmentEpsilonLevel = 3;
            AlignmentIterations = 50;
            AlignmentEccThresholdPercentage = 60;

            ResolutionProportion = 100;
            BorderWidthProportion = 15;
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}