using FreshMvvm;
using Xamarin.Forms;

namespace CrossCam.ViewModel
{
    public class InfoViewModel : FreshBasePageModel
    {
        public Command NavigateToAppExplanationPage { get; set; }

        public Command NavigateToTipsPage { get; set; }

        public Command NavigateToContactPage { get; set; }

        public InfoViewModel()
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
        }
    }
}