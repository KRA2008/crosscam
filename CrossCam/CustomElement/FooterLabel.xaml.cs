using Plugin.DeviceInfo;
using System;
using CrossCam.ViewModel;
using Microsoft.AppCenter.Crashes;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace CrossCam.CustomElement
{
    public partial class FooterLabel
    {
        public FooterLabel()
        {
            InitializeComponent();
            Device.BeginInvokeOnMainThread(() =>
            {
                _versionLabel.Text = "v" + CrossDeviceInfo.Current.AppVersion;
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

                await (BindingContext as BaseViewModel).CoreMethods.DisplayAlert("Could Not Open Link",
                    "The mailto link could not be opened. This could be because your email client is not set up, or some other reason.",
                    "OK");
            }
        }
    }
}