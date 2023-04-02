using Plugin.DeviceInfo;
using System;
using Xamarin.Essentials;

namespace CrossCam.CustomElement
{
    public partial class FooterLabel
    {
        public FooterLabel()
        {
            InitializeComponent();
            _versionLabel.Text = "v" + CrossDeviceInfo.Current.AppVersion;
        }

        private async void TapGestureRecognizer_OnTapped(object sender, EventArgs e)
        {
            await Launcher.OpenAsync("mailto:me@kra2008.com?subject=CrossCam+feedback");
        }
    }
}