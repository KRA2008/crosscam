namespace CrossCam.Model
{
    public class AlignmentSettings : Subsettings
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

        public uint MinimumKeypoints { get; set; }
        public uint KeypointOutlierThresholdTenths { get; set; }

        public uint EccEpsilonLevel { get; set; }
        public uint EccIterations { get; set; }
        public uint EccPyramidLayers { get; set; }
        public uint EccDownsizePercentage { get; set; }
        public uint EccThresholdPercentage { get; set; }
        public uint EccMotionType { get; set; }

        public override void ResetToDefaults()
        {
            IsAutomaticAlignmentOn = true;
            ShowAdvancedAlignmentSettings = false;

            EccDownsizePercentage = 35;
            EccEpsilonLevel = 3;
            EccIterations = 50;
            EccThresholdPercentage = 60;
            EccPyramidLayers = 4;
            EccMotionType = (uint)EccEmguMotionType.Euclidean; //why can't this be the enum? i don't know but it can't.

            DrawKeypointMatches = false;
            UseKeypoints1 = false;
            UseCrossCheck = false;
            DiscardOutliersByDistance = false;
            DiscardOutliersBySlope = true;
            MinimumKeypoints = 5;
            KeypointOutlierThresholdTenths = 20;

            DoKeystoneCorrection = false;
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