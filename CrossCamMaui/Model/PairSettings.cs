namespace CrossCam.Model
{
    public class PairSettings : Subsettings
    {
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

        public uint PairedPreviewFrameDelayMs { get; set; }

        public bool IsFovCorrectionSet { get; set; }
        public float FovPrimaryCorrection { get; set; }
        public float FovSecondaryCorrection { get; set; }

        public uint TimeoutSeconds { get; set; }

        public uint PairSyncSampleCount { get; set; }
        public uint PairedCaptureCountdown { get; set; }

        public uint CaptureMomentExtraDelayMs { get; set; }

        public override void ResetToDefaults()
        {
            //IsPairedPrimary = null; //deliberately do NOT reset this.

            FovPrimaryCorrection = 0;
            FovSecondaryCorrection = 0;
            IsFovCorrectionSet = false;

            PairedPreviewFrameDelayMs = 250;
            PairSyncSampleCount = 50;
            PairedCaptureCountdown = 0;

            CaptureMomentExtraDelayMs = 0;

            TimeoutSeconds = 30;
        }
    }
}