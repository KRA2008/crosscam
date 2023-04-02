using CrossCam.Model;
using Plugin.DeviceInfo;
using Xamarin.Forms;

namespace CrossCam.ViewModel
{
    public class HelpViewModel : BaseViewModel
    {
        public Command NavigateToTechniqueHelpPage { get; set; }
        public Command NavigateToDirectionsPage { get; set; }
        public Command NavigateToTipsPage { get; set; }
        public Command NavigateToContactPage { get; set; }
        public Command NavigateToTipMePage { get; set; }
        private Settings _settings;

        public string AppVersion => "v" + CrossDeviceInfo.Current.AppVersion;

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

            NavigateToContactPage = new Command(async () =>
            {
                await  CoreMethods.PushPageModel<ContactViewModel>();
            });

            NavigateToTipMePage = new Command(async () =>
            {
                await CoreMethods.PushPageModel<TipMeViewModel>();
            });
        }

        public override void Init(object initData)
        {
            base.Init(initData);
            _settings = (Settings) initData;
        }
    }
}