using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CrossCam.Model
{
    public class AlignmentSettings : INotifyPropertyChanged
    {
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

        public bool AlignHorizontallySideBySide { get; set; }
        public bool AlignmentUseKeypoints { get; set; }
        public bool AlignmentUseCrossCheck { get; set; }
        public int AlignmentMinimumKeypoints { get; set; }

        private int _alignmentKeypointOutlierThresholdTenths;
        public int AlignmentKeypointOutlierThresholdTenths
        {
            get => _alignmentKeypointOutlierThresholdTenths;
            set
            {
                if (value > 0)
                {
                    _alignmentKeypointOutlierThresholdTenths = value;
                }
            }
        }

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

        public bool AlignmentDrawMatches { get; set; }

        public void ResetToDefaults()
        {
            IsAutomaticAlignmentOn = true;
            AlignHorizontallySideBySide = true;

            AlignmentDownsizePercentage2 = 35;
            AlignmentEpsilonLevel2 = 3;
            AlignmentIterations2 = 50;
            AlignmentEccThresholdPercentage2 = 60;
            AlignmentPyramidLayers2 = 4;

            AlignmentDrawMatches = false;
            AlignmentUseKeypoints = true;
            AlignmentUseCrossCheck = true;
            AlignmentMinimumKeypoints = 5;
            AlignmentKeypointOutlierThresholdTenths = 10;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}