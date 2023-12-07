using Android.Content;
using CrossCam.Droid.CustomRenderer;
using CrossCam.Wrappers;
using Microsoft.Maui.Controls;
using Microsoft.Maui;

[assembly: Dependency(typeof(LinkSharer))]
namespace CrossCam.Droid.CustomRenderer
{
    public class LinkSharer : ILinkSharer
    {
        public void ShareLink(string link)
        {
            var sendIntent = new Intent();
            sendIntent.SetAction(Intent.ActionSend);
            sendIntent.PutExtra(Intent.ExtraText, link);
            sendIntent.SetType("text/plain");

            var shareIntent = Intent.CreateChooser(sendIntent, (string)null);
            MainActivity.Instance.StartActivity(shareIntent);
        }
    }
}