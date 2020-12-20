using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CrossCam.Model
{
    public class Edits : INotifyPropertyChanged
    {
        private readonly Settings _settings;
        public Edits(Settings settings)
        {
            _settings = settings;
        }

        public double LeftCrop { get; set; }
        public double RightCrop { get; set; }
        public double InsideCrop { get; set; }
        public double OutsideCrop { get; set; }
        public double TopCrop { get; set; }
        public double BottomCrop { get; set; }

        public double VerticalAlignment { get; set; }
        public double LeftZoom { get; set; }
        public double RightZoom { get; set; }
        public float LeftRotation { get; set; }
        public float RightRotation { get; set; }

        public float LeftKeystone { get; set; }
        public float RightKeystone { get; set; }

        public double FovVertAlign { get; set; }
        public double FovRightCorrection
        {
            get => _settings.IsCaptureLeftFirst ? _settings.FovSecondaryCorrection : _settings.FovPrimaryCorrection;
            set
            {
                if (_settings.IsCaptureLeftFirst)
                {
                    _settings.FovSecondaryCorrection = value;
                }
                else
                {
                    _settings.FovPrimaryCorrection = value;
                }
            }
        }
        public double FovLeftCorrection
        {
            get => _settings.IsCaptureLeftFirst ? _settings.FovPrimaryCorrection : _settings.FovSecondaryCorrection;
            set
            {
                if (_settings.IsCaptureLeftFirst)
                {
                    _settings.FovPrimaryCorrection = value;
                }
                else
                {
                    _settings.FovSecondaryCorrection = value;
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}