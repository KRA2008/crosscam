using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CrossCam.Model
{
    public class AlignmentSettings : INotifyPropertyChanged
    {
        public bool UseKeypoints1 { get; set; }
        public bool UseCrossCheck { get; set; }
        public bool DrawKeypointMatches { get; set; }
        public bool DiscardOutliersByDistance { get; set; }
        public bool DiscardOutliersBySlope { get; set; }
        public bool DoKeystoneCorrection { get; set; }

        public bool ShowAdvancedAlignmentSettings { get; set; }

        private bool _isAutomaticAlignmentOn;

        public bool IsAutomaticAlignmentOn
        {
            get => _isAutomaticAlignmentOn;
            set
            {
                _isAutomaticAlignmentOn = value;
                if (!_isAutomaticAlignmentOn)
                {
                    ShowAdvancedAlignmentSettings = false;
                }
            }
        }

        private int _minimumKeypoints;
        public int MinimumKeypoints
        {
            get => _minimumKeypoints;
            set
            {
                if (value > 0)
                {
                    _minimumKeypoints = value;
                }
            }
        }

        private int _keypointOutlierThresholdTenths;
        public int KeypointOutlierThresholdTenths
        {
            get => _keypointOutlierThresholdTenths;
            set
            {
                if (value > 0)
                {
                    _keypointOutlierThresholdTenths = value;
                }
            }
        }

        private int _eccEpsilonLevel;
        public int EccEpsilonLevel
        {
            get => _eccEpsilonLevel;
            set
            {
                if (value > 0)
                {
                    _eccEpsilonLevel = value;
                }
            }
        }

        private int _eccIterations;
        public int EccIterations
        {
            get => _eccIterations;
            set
            {
                if (value > 0)
                {
                    _eccIterations = value;
                }
            }
        }

        private int _eccPyramidLayers;
        public int EccPyramidLayers
        {
            get => _eccPyramidLayers;
            set
            {
                if (value > 0)
                {
                    _eccPyramidLayers = value;
                }
            }
        }

        private int _eccDownsizePercentage;
        public int EccDownsizePercentage
        {
            get => _eccDownsizePercentage;
            set
            {
                if (value > 0)
                {
                    _eccDownsizePercentage = value;
                }
            }
        }

        private int _eccThresholdPercentage;
        public int EccThresholdPercentage
        {
            get => _eccThresholdPercentage;
            set
            {
                if (value > 0)
                {
                    _eccThresholdPercentage = value;
                }
            }
        }

        private int _eccEmguMotionType;
        public int EccMotionType
        {
            get => _eccEmguMotionType;
            set
            {
                if (value >= 0)
                {
                    _eccEmguMotionType = value;
                }
            }
        }

        public void ResetToDefaults()
        {
            IsAutomaticAlignmentOn = true;
            ShowAdvancedAlignmentSettings = false;

            EccDownsizePercentage = 35;
            EccEpsilonLevel = 3;
            EccIterations = 50;
            EccThresholdPercentage = 60;
            EccPyramidLayers = 4;
            EccMotionType = (int)EccEmguMotionType.Euclidean; //why can't this be the enum? i don't know but it can't.

            DrawKeypointMatches = false;
            UseKeypoints1 = false;
            UseCrossCheck = false;
            DiscardOutliersByDistance = false;
            DiscardOutliersBySlope = true;
            MinimumKeypoints = 5;
            KeypointOutlierThresholdTenths = 20;

            DoKeystoneCorrection = false;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public enum EccEmguMotionType
        {
            Translation,
            Euclidean,
            Affine,
            Homography,
        }
    }
}