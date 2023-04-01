using System;
using Xamarin.Essentials;

namespace CrossCam.CustomElement
{
    public partial class EmailMeLabel
    {
        public EmailMeLabel()
        {
            InitializeComponent();
        }

        private async void TapGestureRecognizer_OnTapped(object sender, EventArgs e)
        {
            await Launcher.OpenAsync("mailto:me@kra2008.com?subject=CrossCam+feedback");
        }
    }
}