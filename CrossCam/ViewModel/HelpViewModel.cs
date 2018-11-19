using System;
using FreshMvvm;
using Xamarin.Forms;

namespace CrossCam.ViewModel
{
    public class HelpViewModel : FreshBasePageModel
    {
        public Command NavigateToAppExplanationPage { get; set; }

        public Command NavigateToTipsPage { get; set; }

        public Command NavigateToContactPage { get; set; }

        public Command PrivacyPolicyCommand { get; set; }

        public Command AboutTheDeveloperCommand { get; set; }

        public HelpViewModel()
        {
            NavigateToAppExplanationPage = new Command(async () =>
            {
                await CoreMethods.PushPageModel<AppExplanationViewModel>();
            });

            NavigateToTipsPage = new Command(async () =>
            {
                await CoreMethods.PushPageModel<TipsViewModel>();
            });

            NavigateToContactPage = new Command(async () =>
            {
                await  CoreMethods.PushPageModel<ContactViewModel>();
            });

            PrivacyPolicyCommand = new Command(() =>
            {
                Device.OpenUri(new Uri("http://kra2008.com/crosscam/privacypolicy.html"));
            });

            AboutTheDeveloperCommand = new Command(() =>
            {
                Device.OpenUri(new Uri("http://kra2008.com/"));
            });
        }
    }
}