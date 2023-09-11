using System;
using System.Diagnostics;
using System.Net;
using AVFoundation;
using Foundation;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using UIKit;
using Xamarin.Forms;

namespace CrossCam.iOS
{
	[Register ("AppDelegate")]
	public class AppDelegate : Xamarin.Forms.Platform.iOS.FormsApplicationDelegate
	{
	    private App _app;
        
		public override bool FinishedLaunching (UIApplication app, NSDictionary options)
        {
			Forms.Init(); 
            AppCenter.Start("ef05db4b-0a69-4686-93b0-c0e98b92ac8e", //plz don't abuse this.
                typeof(Analytics), typeof(Crashes));

            _app = new App();
            LoadApplication(_app);
            var success = base.FinishedLaunching(app, options);
            AuthorizeCameraUse();
		    return success;
		}

        public override UIInterfaceOrientationMask GetSupportedInterfaceOrientations(UIApplication application, UIWindow forWindow)
        {
            return UIInterfaceOrientationMask.All;
        }

        public override void ReceiveMemoryWarning(UIApplication application)
        {
			Debug.WriteLine("### LOW MEMORY! OH NO!");
			Analytics.TrackEvent("low memory");
            Debug.WriteLine("state: " + application.ApplicationState);
		}

        public override void WillTerminate(UIApplication uiApplication)
        {
			Debug.WriteLine("### TERMINATING.");
            Debug.WriteLine("state: " + uiApplication.ApplicationState);
		}

        public override void OnResignActivation(UIApplication uiApplication)
        {
			Debug.WriteLine("### RESIGNING ACTIVATION");
            Debug.WriteLine("state: " + uiApplication.ApplicationState);
		}

        public override bool OpenUrl(UIApplication application, NSUrl url, string sourceApplication, NSObject annotation)
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

            _app.LoadSharedImages(Convert.FromBase64String(WebUtility.UrlDecode(image1String)), image2String != null ? Convert.FromBase64String(WebUtility.UrlDecode(image2String)) : null);
            return true;
	    }

        private static async void AuthorizeCameraUse()
	    {
	        var authorizationStatus = AVCaptureDevice.GetAuthorizationStatus(AVMediaType.Video);

	        if (authorizationStatus != AVAuthorizationStatus.Authorized)
	        {
	            await AVCaptureDevice.RequestAccessForMediaTypeAsync(AVMediaType.Video);
	        }
	    }
    }
}

