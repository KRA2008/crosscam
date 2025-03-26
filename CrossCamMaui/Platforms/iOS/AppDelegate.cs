using System.Diagnostics;
using System.Net;
using AVFoundation;
using Foundation;
using UIKit;

namespace CrossCam.Platforms.iOS
{
	[Register ("AppDelegate")]
	public class AppDelegate : MauiUIApplicationDelegate
    {
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

		public override bool FinishedLaunching(UIApplication app, NSDictionary options)
		{
			var success = base.FinishedLaunching(app, options);
			AuthorizeCameraUse();
			return success;
		}

        public override bool OpenUrl(UIApplication application, NSUrl url, NSDictionary options)
	    {
	        var query = url.Query;
	        var image1Start = query.IndexOf("=", StringComparison.Ordinal) + 1;
	        var image1End = query.IndexOf("&", StringComparison.Ordinal);
	        if (image1End == -1)
	        {
	            image1End = query.Length;
	        }
	        var image1String = query.Substring(image1Start, image1End - image1Start);

	        string image2String = null;
	        var image2Start = query.LastIndexOf("=", StringComparison.Ordinal) + 1;
	        if (image2Start != image1Start)
	        {
	            image2String = query.Substring(image2Start, query.Length - image2Start);
	        }

            App.LoadSharedImages(Convert.FromBase64String(WebUtility.UrlDecode(image1String)), image2String != null ? Convert.FromBase64String(WebUtility.UrlDecode(image2String)) : null);
            return true;
	    }

        private static async void AuthorizeCameraUse()
	    {
	        var authorizationStatus = AVCaptureDevice.GetAuthorizationStatus(AVAuthorizationMediaType.Video);

	        if (authorizationStatus != AVAuthorizationStatus.Authorized)
	        {
	            await AVCaptureDevice.RequestAccessForMediaTypeAsync(AVAuthorizationMediaType.Video);
	        }
	    }
    }
}

