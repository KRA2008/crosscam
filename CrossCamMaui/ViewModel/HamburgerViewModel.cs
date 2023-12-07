using CrossCam.Model;
using CrossCam.Wrappers;
using Microsoft.AppCenter.Analytics;

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

            OpenLinkSharer = new Command(() =>
            {
                const string iOSlisting = "https://apps.apple.com/us/app/crosscam/id1436262905";
                const string AndroidListing = "https://play.google.com/store/apps/details?id=com.kra2008.crosscam";

                Analytics.TrackEvent("share link tapped");
                var sharer = DependencyService.Get<ILinkSharer>();

                // TODO Xamarin.Forms.Device.RuntimePlatform is no longer supported. Use Microsoft.Maui.Devices.DeviceInfo.Platform instead. For more details see https://learn.microsoft.com/en-us/dotnet/maui/migration/forms-projects#device-changes
                if (Device.RuntimePlatform == Device.iOS)
                {
                    sharer?.ShareLink(iOSlisting + "\n\n" + AndroidListing);
                }
                else
                {
                    sharer?.ShareLink(AndroidListing + "\n\n" + iOSlisting);
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