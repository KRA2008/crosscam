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

        public float LeftCrop { get; set; }
        public float RightCrop { get; set; }
        public float InsideCrop { get; set; }
        public float OutsideCrop { get; set; }
        public float TopCrop { get; set; }
        public float BottomCrop { get; set; }

        public float VerticalAlignment { get; set; }
        public float LeftZoom { get; set; }
        public float RightZoom { get; set; }
        public float LeftRotation { get; set; }
        public float RightRotation { get; set; }

        public float Keystone { get; set; }

        public float FovRightCorrection
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
        public float FovLeftCorrection
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