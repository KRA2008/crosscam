using CrossCam.Model;
using Xamarin.Forms;

namespace CrossCam.ViewModel
{
    public class HamburgerViewModel : BaseViewModel
    {
        public Command NavigateToHelpPage { get; set; }
        public Command NavigateToTipMePage { get; set; }
        public Command NavigateToContactPage { get; set; }
        public Command NavigateToMorePicturesPage { get; set; }
        private Settings _settings;

        public HamburgerViewModel()
        {
            NavigateToHelpPage = new Command(async () =>
            {
                await CoreMethods.PushPageModel<HelpViewModel>(_settings);
            });

            NavigateToTipMePage = new Command(async () =>
            {
                await CoreMethods.PushPageModel<TipMeViewModel>();
            });

            NavigateToContactPage = new Command(async () =>
            {
                await CoreMethods.PushPageModel<ContactViewModel>();
            });

            NavigateToMorePicturesPage = new Command(async () =>
            {
                await CoreMethods.PushPageModel<SeeMorePicturesViewModel>();
            });
        }

        public override void Init(object initData)
        {
            base.Init(initData);
            _settings = (Settings)initData;
        }
    }
}