using System;
using FreshMvvm;
using Xamarin.Forms;

namespace CrossCam.ViewModel
{
    public class ContactViewModel : FreshBasePageModel
    {
        public Command EmailCommand { get; set; }

        public Command GithubIssueCommand { get; set; }

        public ContactViewModel()
        {
            EmailCommand = new Command(() =>
            {
                Device.OpenUri(new Uri("mailto:me@kra2008.com?subject=CrossCam%20feedback"));
            });

            GithubIssueCommand = new Command(() =>
            {
                Device.OpenUri(new Uri("https://github.com/KRA2008/crosscam/issues"));
            });
        }
    }
}