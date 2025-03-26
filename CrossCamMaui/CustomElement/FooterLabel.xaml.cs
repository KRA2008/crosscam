using CrossCam.Resources.Localization;
using CrossCam.ViewModel;
using CrossCam.Wrappers;

namespace CrossCam.CustomElement
{
    public partial class FooterLabel
    {
        public FooterLabel()
        {
            InitializeComponent();
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _versionLabel.Text = "v" + AppInfo.VersionString;
                _emailMeLabel.TextType = TextType.Html;
                _emailMeLabel.TextType = TextType.Text;
            });
        }

        private async void TapGestureRecognizer_OnTapped(object sender, EventArgs e)
        {
            try
            {
                await Launcher.OpenAsync("mailto:me@kra2008.com?subject=CrossCam+feedback");
            }
            catch (Exception ex)
            {
                Crashes.TrackError(ex);

                await (BindingContext as BaseViewModel).CoreMethods.DisplayAlert(AppResources.Page_Footer_CouldntOpen,
                    AppResources.Page_Footer_OpenError,
                    AppResources.Button_OK);
            }
        }
    }
}