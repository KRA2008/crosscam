using System;
using System.IO;
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

            var items = ExtensionContext.InputItems;
            if (items != null && items.Length > 0)
            {
                var attachments = items.First().Attachments;
                if (attachments != null && attachments.Length > 0)
                {
                    string firstImage = null;
                    string secondImage = null;
                    var firstItemTask = Task.Run(async () =>
                    {
                        var path = await attachments[0].LoadItemAsync(UTType.Image, null);
                        firstImage = WebUtility.UrlEncode(Convert.ToBase64String(File.ReadAllBytes(RemoveFilePrefix(path.ToString()))));
                    });

                    var secondItemTask = Task.CompletedTask;
                    if (attachments.Length > 1)
                    {
                        secondItemTask = Task.Run(async () =>
                        {
                            var path = await attachments[1].LoadItemAsync(UTType.Image, null);
                            secondImage = WebUtility.UrlEncode(Convert.ToBase64String(File.ReadAllBytes(RemoveFilePrefix(path.ToString()))));
                        });
                    }

                    await Task.WhenAll(firstItemTask, secondItemTask);
                    const string BASE_URL = "crosscam://crosscam?";
                    if (firstImage != null)
                    {
                        var secondParameter = secondImage != null
                            ? "&second=" + secondImage
                            : "";
                        var url = new NSUrl(BASE_URL + "first=" + firstImage + secondParameter);
                        UIApplication.SharedApplication.OpenUrl(url);
                        ExtensionContext.CompleteRequest(ExtensionContext.InputItems, null);
                    }
                }
            }
        }

        private static string RemoveFilePrefix(string originalPath)
        {
            const string PREFIX = "file:";
            var index = originalPath.IndexOf(PREFIX, StringComparison.Ordinal);
            return index < 0
                ? originalPath
                : originalPath.Remove(index, PREFIX.Length);
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
