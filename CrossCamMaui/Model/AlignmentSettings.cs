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
                    ShowAdvancedAlignmentSettings1 = false;
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

        public bool ShowAdvancedAlignmentSettings1 { get; set; }
        public uint DownsizePercentage2 { get; set; }

        public bool DrawKeypointMatches { get; set; }
        public bool DrawResultWarpedByOpenCv { get; set; }
        public uint TransformationFindingMethod3 { get; set; }
        public float RatioTest { get; set; }
        public bool ReadModeColor1 { get; set; }
        public bool DiscardOutliersByDistance2 { get; set; }
        public bool DiscardOutliersBySlope2 { get; set; }
        public bool DoKeystoneCorrection1 { get; set; }
        public uint MinimumKeypoints2 { get; set; }
        public uint KeypointOutlierThresholdTenths { get; set; }

        public uint EccEpsilonLevel { get; set; }
        public uint EccIterations { get; set; }
        public uint EccPyramidLayers { get; set; }
        public uint EccThresholdPercentage { get; set; }
        public uint EccMotionType { get; set; }

        public sealed override void ResetToDefaults()
        {
            IsAutomaticAlignmentOn = true;
            ShowAdvancedAlignmentSettings1 = false;

            ForceEcc = false;
            DownsizePercentage2 = 50;
            EccEpsilonLevel = 3;
            EccIterations = 50;
            EccThresholdPercentage = 60;
            EccPyramidLayers = 4;
            EccMotionType = (uint)Model.EccMotionType.Euclidean; //why can't this be the enum? i don't know but it can't.

            ReadModeColor1 = false;
            DrawKeypointMatches = false;
            DrawResultWarpedByOpenCv = false;

            ForceKeypoints2 = false;
            DiscardOutliersByDistance2 = true;
            DiscardOutliersBySlope2 = true;
            MinimumKeypoints2 = 50;
            KeypointOutlierThresholdTenths = 20;
            RatioTest = 0.75f;

            TransformationFindingMethod3 = (uint)TransformationFindingMethod.EstimateRigidPartial; //why not enum?

            DoKeystoneCorrection1 = true;
        }
    }
}