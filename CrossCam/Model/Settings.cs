﻿using System;
using System.ComponentModel;
using CrossCam.ViewModel;

namespace CrossCam.Model
{
    public class Settings : INotifyPropertyChanged
    {
        public const double PARALLEL_BASE_WIDTH = 325;

        private DrawMode _mode;
        public DrawMode Mode
        {
            get => _mode;
            set
            {
                var intValue = (int)value;
                if (intValue < 0) return;

                var totalSaveModes = 0;
                if(SaveForCrossView)
                {
                    totalSaveModes++;
                }
                if (SaveForParallel)
                {
                    totalSaveModes++;
                }
                if (SaveForRedCyanAnaglyph)
                {
                    totalSaveModes++;
                }
                if (SaveForGrayscaleAnaglyph)
                {
                    totalSaveModes++;
                }
                if (SaveSidesSeparately)
                {
                    totalSaveModes++;
                }
                if (SaveRedundantFirstSide)
                {
                    totalSaveModes++;
                }

                if (totalSaveModes == 1)
                {
                    switch (_mode)
                    {
                        case DrawMode.Cross when value != DrawMode.Cross:
                            SaveForCrossView = false;
                            break;
                        case DrawMode.Parallel when value != DrawMode.Parallel:
                            SaveForParallel = false;
                            break;
                        case DrawMode.RedCyanAnaglyph when value != DrawMode.RedCyanAnaglyph:
                            SaveForRedCyanAnaglyph = false;
                            break;
                        case DrawMode.GrayscaleRedCyanAnaglyph when value != DrawMode.GrayscaleRedCyanAnaglyph:
                            SaveForGrayscaleAnaglyph = false;
                            break;
                    }
                }

                switch (value)
                {
                    case DrawMode.Cross:
                        SaveForCrossView = true;
                        break;
                    case DrawMode.Parallel:
                        SaveForParallel = true;
                        break;
                    case DrawMode.RedCyanAnaglyph:
                        SaveForRedCyanAnaglyph = true;
                        break;
                    case DrawMode.GrayscaleRedCyanAnaglyph:
                        SaveForGrayscaleAnaglyph = true;
                        break;
                }

                _mode = value;
            }
        }

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

        public bool SaveForCrossView { get; set; }
        public bool SaveForParallel { get; set; }
        public bool SaveForRedCyanAnaglyph { get; set; }
        public bool SaveForGrayscaleAnaglyph { get; set; }

        public bool SendErrorReports1 { get; set; }

        [Obsolete("Use SaveForRedCyanAnaglyph - kept for backward compatibility")]
        public bool RedCyanAnaglyphMode { get => SaveForRedCyanAnaglyph; set => SaveForRedCyanAnaglyph = value; }
        [Obsolete("Use SaveForGrayscaleAnaglyph - kept for backward compatibility")]
        public bool GrayscaleAnaglyphMode { get => SaveForGrayscaleAnaglyph; set => SaveForGrayscaleAnaglyph = value; }

        public bool AddBorder { get; set; }

        public bool ClipBorderOnNextLoad { get; set; }

        private bool? _isPairedPrimary;
        public bool? IsPairedPrimary
        {
            get => _isPairedPrimary;
            set
            {
                _isPairedPrimary = value;
                IsFovCorrectionSet = false;
                FovPrimaryCorrection = 0;
                FovSecondaryCorrection = 0;
            }
        }

        private int _pairedPreviewFrameDelayMs;
        public int PairedPreviewFrameDelayMs
        {
            get => _pairedPreviewFrameDelayMs;
            set
            {
                if (value >= 0)
                {
                    _pairedPreviewFrameDelayMs = value;
                }
            }
        }

        public bool IsFovCorrectionSet { get; set; }
        public double FovPrimaryCorrection { get; set; }
        public double FovSecondaryCorrection { get; set; }

        private int _pairPreviewSampleCount;
        public int PairSyncSampleCount
        {
            get => _pairPreviewSampleCount;
            set
            {
                if (value > 0)
                {
                    _pairPreviewSampleCount = value;
                }
            }
        }

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

        private int _maximumParallelWidth;
        public int MaximumParallelWidth
        {
            get => _maximumParallelWidth;
            set
            {
                if (value > 0)
                {
                    _maximumParallelWidth = value;
                }
            }
        }

        public AlignmentSettings AlignmentSettings { get; set; }

        public Settings()
        {
            HasOfferedTechniqueHelpBefore = false;
            HasShownDirectionsBefore = false;
            AlignmentSettings = new AlignmentSettings();
            ResetToDefaults();
        }

        public void ResetToDefaults()
        {
            Mode = DrawMode.Cross;

            AreGuideLinesVisible = true;
            IsCaptureLeftFirst = true;
            ShowRollGuide = true;

            AddBorder = false;
            ClipBorderOnNextLoad = false;

            ShowGuideLinesWithFirstCapture = false;
            IsGuideDonutVisible = false;
            ShowGuideDonutWithFirstCapture = false;

            SaveForCrossView = true;
            SaveSidesSeparately = false;
            SaveRedundantFirstSide = false;
            SaveForParallel = false;
            SaveForGrayscaleAnaglyph = false;
            SaveForRedCyanAnaglyph = false;

            IsForceCamera1Enabled = false;
            IsTapToFocusEnabled2 = true;
            IsLockToFirstEnabled = true;

            SavingDirectory = null;
            SaveToExternal = false;

            Handedness = Handedness.Right;

            BorderColor = BorderColor.Black;

            ResolutionProportion = 100;
            BorderWidthProportion = 15;

            MaximumParallelWidth = (int)PARALLEL_BASE_WIDTH;

            //IsPairedPrimary = null; //deliberately do NOT reset this.

            FovPrimaryCorrection = 0;
            FovSecondaryCorrection = 0;
            IsFovCorrectionSet = false;

            PairedPreviewFrameDelayMs = 250;
            PairSyncSampleCount = 50;

            SendErrorReports1 = true; //TODO: consider turning this off when shipping to production

            AlignmentSettings.ResetToDefaults();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public virtual void RaisePropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}