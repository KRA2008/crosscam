using System;
using CrossCam.Model;
using FreshMvvm;
using Plugin.DeviceInfo;
using Xamarin.Forms;

namespace CrossCam.ViewModel
{
    public class HelpViewModel : FreshBasePageModel
    {
        public Command NavigateToTechniqueHelpPage { get; set; }
        public Command NavigateToDirectionsPage { get; set; }
        public Command NavigateToTipsPage { get; set; }
        public Command NavigateToContactPage { get; set; }
        public Command OpenPromotionalAlbumCommand { get; set; }
        public Command PrivacyPolicyCommand { get; set; }
        public Command AboutTheDeveloperCommand { get; set; }
        public Command CrossViewSubredditCommand { get; set; }
        public Command GithubCodeCommand { get; set; }
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
            
            OpenPromotionalAlbumCommand = new Command(() =>
            {
                Device.OpenUri(new Uri("https://imgur.com/a/Crw232n"));
            });

            PrivacyPolicyCommand = new Command(() =>
            {
                Device.OpenUri(new Uri("http://kra2008.com/crosscam/privacypolicy.html"));
            });

            AboutTheDeveloperCommand = new Command(() =>
            {
                Device.OpenUri(new Uri("http://kra2008.com/"));
            });

            CrossViewSubredditCommand = new Command(() =>
            {
                Device.OpenUri(new Uri("https://old.reddit.com/r/CrossView/"));
            });

            GithubCodeCommand = new Command(() =>
            {
                Device.OpenUri(new Uri("https://github.com/KRA2008/crosscam"));
            });
        }

        public override void Init(object initData)
        {
            base.Init(initData);
            _settings = (Settings) initData;
        }
    }
}