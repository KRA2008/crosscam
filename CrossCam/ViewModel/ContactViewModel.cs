using System;
using FreshMvvm;
using Xamarin.Forms;

namespace CrossCam.ViewModel
{
    public class ContactViewModel : FreshBasePageModel
    {
        public Command EmailCommand { get; set; }

        public Command GithubCommand { get; set; }

        public Command CrossViewSubredditCommand { get; set; }

        public ContactViewModel()
        {
            EmailCommand = new Command(() =>
            {
                Device.OpenUri(new Uri("mailto:me@kra2008.com?subject=CrossCam%20feedback"));
            });

            GithubCommand = new Command(() =>
            {
                Device.OpenUri(new Uri("https://github.com/KRA2008/crosscam/issues"));
            });

            CrossViewSubredditCommand = new Command(() =>
            {
                Device.OpenUri(new Uri("https://old.reddit.com/r/CrossView/"));
            });
        }
    }
}