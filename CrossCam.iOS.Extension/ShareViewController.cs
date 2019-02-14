using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using Foundation;
using MobileCoreServices;
using Social;
using UIKit;

namespace CrossCam.iOS.Extension
{
    public partial class ShareViewController : SLComposeServiceViewController
    {
        public ShareViewController(IntPtr handle) : base(handle)
        {
        }

        public override async void ViewDidLoad()
        {
            base.ViewDidLoad();

            // Do any additional setup after loading the view.

            var fetchTask = ExtensionContext.InputItems?.First()?.Attachments?.First()?.LoadItemAsync(UTType.Image, null);
            if (fetchTask != null)
            {
                var item = await fetchTask;

                const string URL = "crosscam://crosscam?";
                Debug.WriteLine("XXX GOT SOMETHING: " + item);
                var url = new NSUrl(URL + WebUtility.UrlEncode(((NSUrl)item).ToString()));
                Debug.WriteLine("XXX OPENING URL: " + url);
                UIApplication.SharedApplication.OpenUrl(url);
                Debug.WriteLine("XXX URL OPENED");
                ExtensionContext.CompleteRequest(ExtensionContext.InputItems, null);
                Debug.WriteLine("XXX REQUEST COMPLETED");
            }
        }

        public override bool IsContentValid()
        {
            // Do validation of contentText and/or NSExtensionContext attachments here
            return true;
        }

        public override SLComposeSheetConfigurationItem[] GetConfigurationItems()
        {
            // To add configuration options via table cells at the bottom of the sheet, return an array of SLComposeSheetConfigurationItem here.
            return new SLComposeSheetConfigurationItem[0];
        }
    }
}
