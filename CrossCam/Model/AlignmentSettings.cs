using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CrossCam.Model
{
    public class AlignmentSettings : INotifyPropertyChanged
    {

        public bool AlignHorizontallySideBySide { get; set; }
        public bool UseKeypoints { get; set; }
        public bool UseCrossCheck { get; set; }
        public bool DrawKeypointMatches { get; set; }
        public bool DiscardOutlierMatches { get; set; }

        private bool _isAutomaticAlignmentOn;
        public bool IsAutomaticAlignmentOn
        {
            get => _isAutomaticAlignmentOn;
            set
            {
                _isAutomaticAlignmentOn = value;
                AlignHorizontallySideBySide = value;
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

        public void ResetToDefaults()
        {
            IsAutomaticAlignmentOn = true;
            AlignHorizontallySideBySide = true;

            EccDownsizePercentage = 35;
            EccEpsilonLevel = 3;
            EccIterations = 50;
            EccThresholdPercentage = 60;
            EccPyramidLayers = 4;

            DrawKeypointMatches = false;
            UseKeypoints = true;
            UseCrossCheck = false;
            DiscardOutlierMatches = true;
            MinimumKeypoints = 5;
            KeypointOutlierThresholdTenths = 20;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}