using CrossCam.iOS.CustomRenderer;
using CrossCam.Wrappers;
using Foundation;
using UIKit;
using Microsoft.Maui.Controls;
using Microsoft.Maui;

[assembly: Dependency(typeof(LinkSharer))]
namespace CrossCam.iOS.CustomRenderer
{
    public class LinkSharer : ILinkSharer
    {
        public void ShareLink(string link)
        {
            var item = NSObject.FromObject(link);
            var activityItems = new[] { item };
            UIActivity[] applicationActivities = null;

            var activityController = new UIActivityViewController(activityItems, applicationActivities);

            UIApplication.SharedApplication.KeyWindow?.RootViewController?.PresentViewController(activityController,
                true, null);
        }
    }
}