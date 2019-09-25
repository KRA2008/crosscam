using System;
using Xamarin.Forms;

namespace CrossCam.CustomElement
{
    public partial class EmailMeLabel
    {
        public EmailMeLabel()
        {
            InitializeComponent();
        }

        private void TapGestureRecognizer_OnTapped(object sender, EventArgs e)
        {
            Device.OpenUri(new Uri("mailto:me@kra2008.com?subject=CrossCam+feedback"));
        }
    }
}