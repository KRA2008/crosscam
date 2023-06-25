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

        public float Keystone { get; set; }

        public double FovRightCorrection
        {
            get => _settings.IsCaptureLeftFirst ? _settings.PairSettings.FovSecondaryCorrection : _settings.PairSettings.FovPrimaryCorrection;
            set
            {
                if (_settings.IsCaptureLeftFirst)
                {
                    _settings.PairSettings.FovSecondaryCorrection = value;
                    _settings.PairSettings.FovPrimaryCorrection = 0;
                    OnPropertyChanged(nameof(FovLeftCorrection));
                }
                else
                {
                    _settings.PairSettings.FovPrimaryCorrection = value;
                    _settings.PairSettings.FovSecondaryCorrection = 0;
                    OnPropertyChanged(nameof(FovLeftCorrection));
                }
            }
        }
        public double FovLeftCorrection
        {
            get => _settings.IsCaptureLeftFirst ? _settings.PairSettings.FovPrimaryCorrection : _settings.PairSettings.FovSecondaryCorrection;
            set
            {
                if (_settings.IsCaptureLeftFirst)
                {
                    _settings.PairSettings.FovPrimaryCorrection = value;
                    _settings.PairSettings.FovSecondaryCorrection = 0;
                    OnPropertyChanged(nameof(FovRightCorrection));
                }
                else
                {
                    _settings.PairSettings.FovSecondaryCorrection = value;
                    _settings.PairSettings.FovPrimaryCorrection = 0;
                    OnPropertyChanged(nameof(FovRightCorrection));
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