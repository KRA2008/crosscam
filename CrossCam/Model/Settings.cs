using System;
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
                if (SaveForCardboard)
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
                        case DrawMode.Cardboard when value != DrawMode.Cardboard:
                            SaveForCardboard = false;
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
                    case DrawMode.Cardboard:
                        SaveForCardboard = true;
                        break;
                }

                _mode = value;
            }
        }

        public bool HasOfferedTechniqueHelpBefore
        {
            get => _hasOfferedTechniqueHelpBefore;
            set => _hasOfferedTechniqueHelpBefore = value;
        }

        public bool HasShownDirectionsBefore
        {
            get => _hasShownDirectionsBefore;
            set => _hasShownDirectionsBefore = value;
        }

        public bool ShowGuideLinesWithFirstCapture
        {
            get => _showGuideLinesWithFirstCapture;
            set => _showGuideLinesWithFirstCapture = value;
        }

        public bool ShowGuideDonutWithFirstCapture
        {
            get => _showGuideDonutWithFirstCapture;
            set => _showGuideDonutWithFirstCapture = value;
        }

        public bool ShowRollGuide
        {
            get => _showRollGuide;
            set => _showRollGuide = value;
        }

        public bool ShowPreviewFuseGuide
        {
            get => _showPreviewFuseGuide;
            set => _showPreviewFuseGuide = value;
        }

        public bool IsCaptureLeftFirst
        {
            get => _isCaptureLeftFirst;
            set => _isCaptureLeftFirst = value;
        }

        private bool _isForceCamera1Enabled;
        public bool IsForceCamera1Enabled
        {
            get => _isForceCamera1Enabled;
            set
            {
                if (value)
                {
                    IsForceCamera2Enabled = false;
                }
                _isForceCamera1Enabled = value;
            }
        }

        private bool _isForceCamera2Enabled;
        public bool IsForceCamera2Enabled
        {
            get => _isForceCamera2Enabled;
            set
            {
                if (value)
                {
                    IsForceCamera1Enabled = false;
                }
                _isForceCamera2Enabled = value;
            }
        }

        public bool IsLockToFirstEnabled
        {
            get => _isLockToFirstEnabled;
            set => _isLockToFirstEnabled = value;
        }

        public bool IsTapToFocusEnabled2
        {
            get => _isTapToFocusEnabled2;
            set => _isTapToFocusEnabled2 = value;
        }

        public bool SaveRedundantFirstSide
        {
            get => _saveRedundantFirstSide;
            set => _saveRedundantFirstSide = value;
        }

        public string SavingDirectory
        {
            get => _savingDirectory;
            set => _savingDirectory = value;
        }

        public bool SaveToExternal
        {
            get => _saveToExternal;
            set => _saveToExternal = value;
        }

        public bool SaveForCrossView
        {
            get => _saveForCrossView;
            set => _saveForCrossView = value;
        }

        public bool SaveForParallel
        {
            get => _saveForParallel;
            set => _saveForParallel = value;
        }

        public bool SaveForRedCyanAnaglyph
        {
            get => _saveForRedCyanAnaglyph;
            set => _saveForRedCyanAnaglyph = value;
        }

        public bool SaveForGrayscaleAnaglyph
        {
            get => _saveForGrayscaleAnaglyph;
            set => _saveForGrayscaleAnaglyph = value;
        }

        public bool SaveForTriple
        {
            get => _saveForTriple;
            set => _saveForTriple = value;
        }

        public bool SaveForQuad
        {
            get => _saveForQuad;
            set => _saveForQuad = value;
        }

        public bool SaveWithFuseGuide
        {
            get => _saveWithFuseGuide;
            set => _saveWithFuseGuide = value;
        }

        private bool _saveForCardboard;
        public bool SaveForCardboard
        {
            get => _saveForCardboard;
            set
            {
                _saveForCardboard = value;
                if (_saveForCardboard)
                {
                    SaveSidesSeparately = true;
                }
            }
        }

        public bool SendErrorReports1
        {
            get => _sendErrorReports1;
            set => _sendErrorReports1 = value;
        }

        [Obsolete("Use SaveForRedCyanAnaglyph - kept for backward compatibility")]
        public bool RedCyanAnaglyphMode { get => SaveForRedCyanAnaglyph; set => SaveForRedCyanAnaglyph = value; }
        [Obsolete("Use SaveForGrayscaleAnaglyph - kept for backward compatibility")]
        public bool GrayscaleAnaglyphMode { get => SaveForGrayscaleAnaglyph; set => SaveForGrayscaleAnaglyph = value; }

        public bool AddBorder
        {
            get => _addBorder;
            set => _addBorder = value;
        }

        public bool ClipBorderOnNextLoad
        {
            get => _clipBorderOnNextLoad;
            set => _clipBorderOnNextLoad = value;
        }

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

        public bool IsFovCorrectionSet
        {
            get => _isFovCorrectionSet;
            set => _isFovCorrectionSet = value;
        }

        public double FovPrimaryCorrection
        {
            get => _fovPrimaryCorrection;
            set => _fovPrimaryCorrection = value;
        }

        public double FovSecondaryCorrection
        {
            get => _fovSecondaryCorrection;
            set => _fovSecondaryCorrection = value;
        }

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

        private int _pairedCaptureCountdown;
        public int PairedCaptureCountdown
        {
            get => _pairedCaptureCountdown;
            set
            {
                if (value < 0) return;
                _pairedCaptureCountdown = value;
            }
        }

        private PortraitCaptureButtonPosition _portraitPortraitCaptureButtonPosition;
        public PortraitCaptureButtonPosition PortraitCaptureButtonPosition
        {
            get => _portraitPortraitCaptureButtonPosition;
            set
            {
                var intValue = (int)value;
                if (intValue < 0) return;
                _portraitPortraitCaptureButtonPosition = value;
            }
        }

        private LandscapeCaptureButtonHorizontalPosition _landscapeCaptureButtonHorizontalPosition;
        public LandscapeCaptureButtonHorizontalPosition LandscapeCaptureButtonHorizontalPosition
        {
            get => _landscapeCaptureButtonHorizontalPosition;
            set
            {
                var intValue = (int)value;
                if (intValue < 0) return;
                _landscapeCaptureButtonHorizontalPosition = value;
            }
        }

        private LandscapeCaptureButtonVerticalPosition _landscapeCaptureButtonVerticalPosition;
        public LandscapeCaptureButtonVerticalPosition LandscapeCaptureButtonVerticalPosition
        {
            get => _landscapeCaptureButtonVerticalPosition;
            set
            {
                var intValue = (int)value;
                if (intValue < 0) return;
                _landscapeCaptureButtonVerticalPosition = value;
            }
        }

        private PairButtonHorizontalPosition _pairButtonHorizontalPosition;
        public PairButtonHorizontalPosition PairButtonHorizontalPosition
        {
            get => _pairButtonHorizontalPosition;
            set
            {
                var intValue = (int)value;
                if (intValue < 0) return;
                _pairButtonHorizontalPosition = value;
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

                if (!value)
                {
                    SaveForCardboard = false;
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
        private bool _hasOfferedTechniqueHelpBefore;
        private bool _hasShownDirectionsBefore;
        private bool _showGuideLinesWithFirstCapture;
        private bool _showGuideDonutWithFirstCapture;
        private bool _showRollGuide;
        private bool _showPreviewFuseGuide;
        private bool _isCaptureLeftFirst;
        private bool _isLockToFirstEnabled;
        private bool _isTapToFocusEnabled2;
        private bool _saveRedundantFirstSide;
        private string _savingDirectory;
        private bool _saveToExternal;
        private bool _saveForCrossView;
        private bool _saveForParallel;
        private bool _saveForRedCyanAnaglyph;
        private bool _saveForGrayscaleAnaglyph;
        private bool _saveForTriple;
        private bool _saveForQuad;
        private bool _saveWithFuseGuide;
        private bool _sendErrorReports1;
        private bool _addBorder;
        private bool _clipBorderOnNextLoad;
        private bool _isFovCorrectionSet;
        private double _fovPrimaryCorrection;
        private double _fovSecondaryCorrection;
        private AlignmentSettings _alignmentSettings;

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

        private int _cardboardIpd;
        public int CardboardIpd
        {
            get => _cardboardIpd;
            set
            {
                if (value > 0)
                {
                    _cardboardIpd = value;
                }
            }
        }

        private int _cardboardBarrelDistortion;
        public int CardboardBarrelDistortion
        {
            get => _cardboardBarrelDistortion;
            set
            {
                if (value > 0)
                {
                    _cardboardBarrelDistortion = value;
                }
            }
        }

        public AlignmentSettings AlignmentSettings
        {
            get => _alignmentSettings;
            set => _alignmentSettings = value;
        }

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
            ShowPreviewFuseGuide = true;

            SaveForCrossView = true;
            SaveSidesSeparately = false;
            SaveRedundantFirstSide = false;
            SaveForParallel = false;
            SaveForGrayscaleAnaglyph = false;
            SaveForRedCyanAnaglyph = false;
            SaveForTriple = false;
            SaveForQuad = false;
            SaveWithFuseGuide = false;
            SaveForCardboard = false;

            IsForceCamera1Enabled = false;
            IsForceCamera2Enabled = false;
            IsTapToFocusEnabled2 = true;
            IsLockToFirstEnabled = true;

            SavingDirectory = null;
            SaveToExternal = false;

            PortraitCaptureButtonPosition = PortraitCaptureButtonPosition.Middle;
            LandscapeCaptureButtonHorizontalPosition = LandscapeCaptureButtonHorizontalPosition.HomeEnd;
            LandscapeCaptureButtonVerticalPosition = LandscapeCaptureButtonVerticalPosition.Bottom;
            PairButtonHorizontalPosition = PairButtonHorizontalPosition.Left;

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
            PairedCaptureCountdown = 0; //TODO: 0 or 3?

            CardboardIpd = 400;
            CardboardBarrelDistortion = 200;

            SendErrorReports1 = true;

            AlignmentSettings.ResetToDefaults();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public virtual void RaisePropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}