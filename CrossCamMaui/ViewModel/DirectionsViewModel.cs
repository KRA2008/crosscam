using CrossCam.Model;
using CrossCam.Resources.Localization;

namespace CrossCam.ViewModel
{
    public class DirectionsViewModel : BaseViewModel
    {
        private Settings _settings;

        public string ViewModeIng
        {
            get
            {
                return _settings.Mode switch
                {
                    DrawMode.Parallel => AppResources.Page_HowToUse_2parallel,
                    DrawMode.Cross => AppResources.Page_HowToUse_2cross,
                    DrawMode.RedCyanAnaglyph => AppResources.Page_HowToUse_2anaglyph,
                    DrawMode.GrayscaleRedCyanAnaglyph => AppResources.Page_HowToUse_2anaglyph,
                    DrawMode.Cardboard => AppResources.Page_HowToUse_2cardboard,
                    _ => throw new ArgumentOutOfRangeException()
                };
            }
        }

        public override void Init(object initData)
        {
            base.Init(initData);
            _settings = (Settings)initData;
        }
    }
}