namespace CrossCam.Model
{
    public class AlignmentSettings : Subsettings
    {
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
        public bool UseKeypoints1 { get; set; }
        public bool ShowAdvancedAlignmentSettings { get; set; }
        public uint DownsizePercentage { get; set; }

        public bool UseCrossCheck { get; set; }
        public bool DrawKeypointMatches { get; set; }
        public bool DrawResultWarpedByOpenCv { get; set; }
        public uint TransformationFindingMethod { get; set; }
        public float RatioTest { get; set; }
        public float PhysicalDistanceThreshold { get; set; }
        public bool ReadModeColor { get; set; }
        public bool DiscardOutliersByDistance { get; set; }
        public bool DiscardOutliersBySlope1 { get; set; }
        public bool DoKeystoneCorrection1 { get; set; }
        public uint MinimumKeypoints1 { get; set; }
        public uint KeypointOutlierThresholdTenths { get; set; }

        public uint EccEpsilonLevel { get; set; }
        public uint EccIterations { get; set; }
        public uint EccPyramidLayers { get; set; }
        public uint EccThresholdPercentage { get; set; }
        public uint EccMotionType { get; set; }

        public override void ResetToDefaults()
        {
            IsAutomaticAlignmentOn = true;
            ShowAdvancedAlignmentSettings = false;

            DownsizePercentage = 35;
            EccEpsilonLevel = 3;
            EccIterations = 50;
            EccThresholdPercentage = 60;
            EccPyramidLayers = 4;
            EccMotionType = (uint)Model.EccMotionType.Euclidean; //why can't this be the enum? i don't know but it can't.

            ReadModeColor = true;
            DrawKeypointMatches = false;
            DrawResultWarpedByOpenCv = false;
            UseKeypoints1 = false;
            UseCrossCheck = false;
            DiscardOutliersByDistance = false;
            DiscardOutliersBySlope1 = false;
            MinimumKeypoints1 = 15;
            KeypointOutlierThresholdTenths = 20;
            RatioTest = 0.75f;
            PhysicalDistanceThreshold = 0.25f;

            TransformationFindingMethod = (uint)Model.TransformationFindingMethod.BinarySearch; //why not enum?

            DoKeystoneCorrection1 = true;
        }
    }
}