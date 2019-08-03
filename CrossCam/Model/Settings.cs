﻿using System.ComponentModel;

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
        public bool IsTapToFocusEnabled2 { get; set; }

        public bool SaveRedundantFirstSide { get; set; }

        public string SavingDirectory { get; set; }
        public bool SaveToExternal { get; set; }

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

        private Handedness _handedness;
        public Handedness Handedness
        {
            get => _handedness;
            set
            {
                var intValue = (int)value;
                if (intValue < 0) return;
                _handedness = value;
            }
        }

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

        private int _alignmentEpsilonLevel2;
        public int AlignmentEpsilonLevel2
        {
            get => _alignmentEpsilonLevel2;
            set
            {
                if (value > 0)
                {
                    _alignmentEpsilonLevel2 = value;
                }
            }
        }

        private int _alignmentIterations2;
        public int AlignmentIterations2
        {
            get => _alignmentIterations2;
            set
            {
                if (value > 0)
                {
                    _alignmentIterations2 = value;
                }
            }
        }

        private int _alignmentPyramidLayers2;
        public int AlignmentPyramidLayers2
        {
            get => _alignmentPyramidLayers2;
            set
            {
                if (value > 0)
                {
                    _alignmentPyramidLayers2 = value;
                }
            }
        }

        private int _alignmentDownsizePercentage2;
        public int AlignmentDownsizePercentage2
        {
            get => _alignmentDownsizePercentage2;
            set
            {
                if (value > 0)
                {
                    _alignmentDownsizePercentage2 = value;
                }
            }
        }

        private int _alignmentEccThresholdPercentage2;
        public int AlignmentEccThresholdPercentage2
        {
            get => _alignmentEccThresholdPercentage2;
            set
            {
                if (value > 0)
                {
                    _alignmentEccThresholdPercentage2 = value;
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
            IsTapToFocusEnabled2 = true;
            IsLockToFirstEnabled = true;

            SavingDirectory = null;
            SaveToExternal = false;

            Handedness = Handedness.Right;

            BorderColor = BorderColor.Black;

            IsAutomaticAlignmentOn = true;
            AlignmentDownsizePercentage2 = 35;
            AlignmentEpsilonLevel2 = 3;
            AlignmentIterations2 = 50;
            AlignmentEccThresholdPercentage2 = 60;
            AlignmentPyramidLayers2 = 4;

            ResolutionProportion = 100;
            BorderWidthProportion = 15;
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}