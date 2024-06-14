namespace CrossCam.Model
{
    public class AlignmentSettings : Subsettings
    {
        public AlignmentSettings()
        {
            ResetToDefaults();
        }

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
        private bool _forceKeypoints2;
        public bool ForceKeypoints2
        {
            get => _forceKeypoints2;
            set
            {
                _forceKeypoints2 = value;
                if (value)
                {
                    ForceEcc = false;
                }
            }
        }

        private bool _forceEcc;
        public bool ForceEcc
        {
            get => _forceEcc;
            set
            {
                _forceEcc = value;
                if (value)
                {
                    ForceKeypoints2 = false;
                }
            }
        }

        public bool ShowAdvancedAlignmentSettings { get; set; }
        public uint DownsizePercentage { get; set; }

        public bool DrawKeypointMatches { get; set; }
        public bool DrawResultWarpedByOpenCv { get; set; }
        public uint TransformationFindingMethod2 { get; set; }
        public float RatioTest { get; set; }
        public float PhysicalDistanceThreshold { get; set; }
        public bool ReadModeColor { get; set; }
        public bool DiscardOutliersByDistance2 { get; set; }
        public bool DiscardOutliersBySlope2 { get; set; }
        public bool DoKeystoneCorrection1 { get; set; }
        public uint MinimumKeypoints1 { get; set; }
        public uint KeypointOutlierThresholdTenths { get; set; }

        public uint EccEpsilonLevel { get; set; }
        public uint EccIterations { get; set; }
        public uint EccPyramidLayers { get; set; }
        public uint EccThresholdPercentage { get; set; }
        public uint EccMotionType { get; set; }

        public sealed override void ResetToDefaults()
        {
            IsAutomaticAlignmentOn = true;
            ShowAdvancedAlignmentSettings = false;

            ForceEcc = false;
            DownsizePercentage = 35;
            EccEpsilonLevel = 3;
            EccIterations = 50;
            EccThresholdPercentage = 60;
            EccPyramidLayers = 4;
            EccMotionType = (uint)Model.EccMotionType.Euclidean; //why can't this be the enum? i don't know but it can't.

            ReadModeColor = true;
            DrawKeypointMatches = false;
            DrawResultWarpedByOpenCv = false;

            ForceKeypoints2 = false;
            DiscardOutliersByDistance2 = true;
            DiscardOutliersBySlope2 = true;
            MinimumKeypoints1 = 15;
            KeypointOutlierThresholdTenths = 20;
            RatioTest = 0.75f;
            PhysicalDistanceThreshold = 0.25f;

            TransformationFindingMethod2 = (uint)Model.TransformationFindingMethod.FindHomography; //why not enum?

            DoKeystoneCorrection1 = true;
        }
    }
}