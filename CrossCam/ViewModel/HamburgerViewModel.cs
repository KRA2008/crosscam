using System.Collections.Generic;
using CrossCam.Model;
using CrossCam.Wrappers;
using Microsoft.AppCenter.Analytics;
using Xamarin.Forms;

namespace CrossCam.ViewModel
{
    public class HamburgerViewModel : BaseViewModel
    {
        public Command NavigateToHelpPage { get; set; }
        public Command NavigateToTipMePage { get; set; }
        public Command NavigateToContactPage { get; set; }
        public Command NavigateToMorePicturesPage { get; set; }
        public Command OpenLinkSharer { get; set; }
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

            OpenLinkSharer = new Command(async () =>
            {
                const string iOS = "Apple App Store";
                const string Android = "Google Play app store";
                var whichLink = await CoreMethods.DisplayActionSheet("Share link to " + iOS + " or "+ Android +"?", null, null,
                    iOS, Android);
                if (whichLink == iOS)
                {
                    Analytics.TrackEvent("share link tapped", new Dictionary<string, string> {{"platform", "iOS"}});
                    DependencyService.Get<ILinkSharer>()?.ShareLink("https://apps.apple.com/us/app/crosscam/id1436262905");
                } 
                else if (whichLink == Android)
                {
                    Analytics.TrackEvent("share link tapped", new Dictionary<string, string> {{"platform", "Android"}});
                    DependencyService.Get<ILinkSharer>()?.ShareLink("https://play.google.com/store/apps/details?id=com.kra2008.crosscam");
                }
            });
        }

        public override void Init(object initData)
        {
            base.Init(initData);
            _settings = (Settings)initData;
        }
    }
}