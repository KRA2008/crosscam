using System;
using FreshMvvm;
using Xamarin.Forms;

namespace CrossCam.ViewModel
{
    public class HelpViewModel : FreshBasePageModel
    {
        public Command NavigateToTechniqueHelpPage { get; set; }

        public Command NavigateToDirectionsPage { get; set; }

        public Command NavigateToTipsPage { get; set; }

        public Command NavigateToContactPage { get; set; }

        public Command PrivacyPolicyCommand { get; set; }

        public Command AboutTheDeveloperCommand { get; set; }

        public HelpViewModel()
        {
            NavigateToTechniqueHelpPage = new Command(async () =>
            {
                await CoreMethods.PushPageModel<TechniqueHelpViewModel>();
            });

            NavigateToDirectionsPage = new Command(async () =>
            {
                await CoreMethods.PushPageModel<DirectionsViewModel>();
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