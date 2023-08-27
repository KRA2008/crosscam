namespace CrossCam.Model
{
    public class CardboardSettings : Subsettings
    {
        private bool _addBarrelDistortion;
        public bool AddBarrelDistortion
        {
            get => _addBarrelDistortion;
            set
            {
                _addBarrelDistortion = value;
                if (!value)
                {
                    AddBarrelDistortionFinalOnly = false;
                    CardboardDownsize = false;
                }
            }
        }

        public uint CardboardIpd { get; set; }
        public uint CardboardBarrelDistortion { get; set; }
        public bool CardboardDownsize { get; set; }
        public uint CardboardDownsizePercentage { get; set; }
        public bool ImmersiveCardboardFinal { get; set; }
        public bool AddBarrelDistortionFinalOnly { get; set; }

        public override void ResetToDefaults()
        {
            CardboardIpd = 400;
            CardboardBarrelDistortion = 200;
            AddBarrelDistortion = false;
            AddBarrelDistortionFinalOnly = false;
            CardboardDownsize = false;
            CardboardDownsizePercentage = 50;
            ImmersiveCardboardFinal = true;
        }
    }
}