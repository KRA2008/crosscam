using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
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
            var firstItemFetch = Task.FromResult((NSObject)null);
            var secondItemFetch = Task.FromResult((NSObject)null);
            Debug.WriteLine("### 1");
            var items = ExtensionContext.InputItems;
            if (items != null && items.Length > 0)
            {
                Debug.WriteLine("### 2");
                var attachments = items.First().Attachments;
                if (attachments != null)
                {
                    Debug.WriteLine("### 3");
                    if (attachments.Length > 0)
                    {
                        Debug.WriteLine("### 4");
                        firstItemFetch = attachments[0].LoadItemAsync(UTType.Image, null);
                    }
                    if (attachments.Length > 1)
                    {
                        Debug.WriteLine("### 5");
                        secondItemFetch = attachments[1].LoadItemAsync(UTType.Image, null);
                    }
                }
            }
            Debug.WriteLine("### 6");

            await Task.WhenAll(firstItemFetch, secondItemFetch);
            Debug.WriteLine("### 7");
            const string BASE_URL = "crosscam://crosscam?";
            if (firstItemFetch.Result != null)
            {
                Debug.WriteLine("### 8");
                var secondParameter = secondItemFetch.Result != null
                    ? "&second=" + WebUtility.UrlEncode(secondItemFetch.Result.ToString())
                    : "";
                Debug.WriteLine("### 9");
                var url = new NSUrl(BASE_URL + "first=" + WebUtility.UrlEncode(firstItemFetch.Result.ToString()) + secondParameter);
                Debug.WriteLine("### 10");
                UIApplication.SharedApplication.OpenUrl(url);
                ExtensionContext.CompleteRequest(ExtensionContext.InputItems, null);
                Debug.WriteLine("### 11");
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
