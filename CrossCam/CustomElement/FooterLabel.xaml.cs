using Plugin.DeviceInfo;
using System;
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
            await Launcher.OpenAsync("mailto:me@kra2008.com?subject=CrossCam+feedback");
        }
    }
}