using CrossCam.Model;
using FreshMvvm;

namespace CrossCam.ViewModel
{
    public class DirectionsViewModel : FreshBasePageModel
    {
        private Settings _settings;
        public string ViewModeIng => (_settings.Mode == DrawMode.Parallel ? "parallel" : "cross") + " viewing";
        public string DirectionToMove
        {
            get
            {
                if (_settings.IsCaptureLeftFirst && _settings.Mode == DrawMode.Parallel ||
                    !_settings.IsCaptureLeftFirst && _settings.Mode != DrawMode.Parallel)
                    return "RIGHT";
                return "LEFT";
            }
        }

        public override void Init(object initData)
        {
            base.Init(initData);
            _settings = (Settings)initData;
        }
    }
}