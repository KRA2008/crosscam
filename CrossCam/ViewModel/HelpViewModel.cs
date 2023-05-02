using CrossCam.Model;
using Xamarin.Forms;

namespace CrossCam.ViewModel
{
    public class HelpViewModel : BaseViewModel
    {
        public Command NavigateToTechniqueHelpPage { get; set; }
        public Command NavigateToDirectionsPage { get; set; }
        public Command NavigateToTipsPage { get; set; }
        public Command NavigateToFAQPage { get; set; }
        private Settings _settings;

        public HelpViewModel()
        {
            NavigateToTechniqueHelpPage = new Command(async () =>
            {
                await CoreMethods.PushPageModel<TechniqueHelpViewModel>(_settings);
            });

            NavigateToDirectionsPage = new Command(async () =>
            {
                await CoreMethods.PushPageModel<DirectionsViewModel>(_settings);
            });

            NavigateToTipsPage = new Command(async () =>
            {
                await CoreMethods.PushPageModel<TipsViewModel>();
            });

            NavigateToFAQPage = new Command(async () =>
            {
                await CoreMethods.PushPageModel<FAQViewModel>();
            });
        }

        public override void Init(object initData)
        {
            base.Init(initData);
            _settings = (Settings) initData;
        }
    }
}