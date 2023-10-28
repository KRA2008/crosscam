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
                if (SaveForCrossView)
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
        
        public bool HasOfferedTechniqueHelpBefore2 { get; set; }
        public bool HasShownDirectionsBefore { get; set; }
        public bool ShowRollGuide { get; set; }
        public bool FullscreenCapturing { get; set; }
        public bool FullscreenEditing { get; set; }
        public bool ShowPreviewFuseGuide { get; set; }
        public bool IsCaptureLeftFirst { get; set; }
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

        public bool IsCaptureInMirrorMode { get; set; }
        public bool IsLockToFirstEnabled { get; set; }
        public bool IsTapToFocusEnabled2 { get; set; }
        public bool SaveRedundantFirstSide { get; set; }
        public bool SaveForCrossView { get; set; }
        public bool SaveForParallel { get; set; }
        public bool SaveForRedCyanAnaglyph { get; set; }
        public bool SaveForGrayscaleAnaglyph { get; set; }
        public bool SaveForTriple { get; set; }
        public bool SaveForQuad { get; set; }
        public bool SaveWithFuseGuide { get; set; }
        public bool SaveForCardboard { get; set; }
        public bool ClearCapturesAfterSave { get; set; }

        private bool _saveIntoDedicatedFolder;
        public bool SaveIntoDedicatedFolder
        {
            get => _saveIntoDedicatedFolder;
            set
            {
                _saveIntoDedicatedFolder = value;
                if (value)
                {
                    SaveIntoSeparateFolders = false;
                }
            }
        }

        private bool _saveIntoSeparateFolders;
        public bool SaveIntoSeparateFolders
        {
            get => _saveIntoSeparateFolders;
            set
            {
                _saveIntoSeparateFolders = value;
                if (value)
                {
                    SaveIntoDedicatedFolder = false;
                }
            }
        }

        public string SavingDirectory { get; set; }
        public bool SaveToExternal { get; set; }

        public bool PromptForErrorEmails { get; set; }
        public bool IsAnalyticsEnabled { get; set; }

        [Obsolete("Use SaveForRedCyanAnaglyph - kept for backward compatibility")]
        public bool RedCyanAnaglyphMode { get => SaveForRedCyanAnaglyph; set => SaveForRedCyanAnaglyph = value; }
        [Obsolete("Use SaveForGrayscaleAnaglyph - kept for backward compatibility")]
        public bool GrayscaleAnaglyphMode { get => SaveForGrayscaleAnaglyph; set => SaveForGrayscaleAnaglyph = value; }

        public bool AddBorder2 { get; set; }
        public bool ClipBorderOnNextLoad { get; set; }

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
                var intValue = (int)value;
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
                    AreGuideLinesColored = false;
                }
            }
        }

        public bool AreGuideLinesColored { get; set; }

        public bool IsGuideDonutVisible { get; set; }

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

        public uint ResolutionProportion { get; set; }
        public uint BorderWidthProportion { get; set; }
        public uint MaximumParallelWidth { get; set; }

        public CardboardSettings CardboardSettings { get; set; }
        public AlignmentSettings AlignmentSettings { get; set; }
        public EditsSettings EditsSettings { get; set; }
        public PairSettings PairSettings { get; set; }

        public Settings()
        {
            HasOfferedTechniqueHelpBefore2 = false;
            HasShownDirectionsBefore = false;
            AlignmentSettings = new AlignmentSettings();
            EditsSettings = new EditsSettings();
            CardboardSettings = new CardboardSettings();
            PairSettings = new PairSettings();
            ResetToDefaults();
        }

        public void ResetToDefaults()
        {
            Mode = DrawMode.Cross;

            AreGuideLinesVisible = true;
            AreGuideLinesColored = false;
            IsCaptureLeftFirst = true;
            ShowRollGuide = true;
            IsCaptureInMirrorMode = false;

            AddBorder2 = true;
            ClipBorderOnNextLoad = false;
            
            IsGuideDonutVisible = false;
            ShowPreviewFuseGuide = true;
            FullscreenCapturing = false;
            FullscreenEditing = false;

            SaveForCrossView = true;
            SaveSidesSeparately = false;
            SaveRedundantFirstSide = false;
            SaveForParallel = false;
            SaveForGrayscaleAnaglyph = false;
            SaveForRedCyanAnaglyph = false;
            SaveForTriple = false;
            SaveForQuad = false;
            SaveWithFuseGuide = true;
            SaveForCardboard = false;
            ClearCapturesAfterSave = true;


            IsForceCamera1Enabled = false;
            IsForceCamera2Enabled = false;
            IsTapToFocusEnabled2 = true;
            IsLockToFirstEnabled = true;

            SaveIntoDedicatedFolder = true;
            SaveIntoSeparateFolders = false;
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

            PromptForErrorEmails = false;
            IsAnalyticsEnabled = true;

            AlignmentSettings.ResetToDefaults();
            EditsSettings.ResetToDefaults();
            CardboardSettings.ResetToDefaults();
            PairSettings.ResetToDefaults();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public virtual void RaisePropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}