using AVFoundation;
using Foundation;
using UIKit;
using Xamarin.Forms;

namespace CrossCam.iOS
{
	[Register ("AppDelegate")]
	public partial class AppDelegate : Xamarin.Forms.Platform.iOS.FormsApplicationDelegate
	{
		public override bool FinishedLaunching (UIApplication app, NSDictionary options)
		{
			Forms.Init();

            LoadApplication(new App());
            var success = base.FinishedLaunching(app, options);
            AuthorizeCameraUse();
		    return success;
		}

	    public override bool OpenUrl(UIApplication application, NSUrl url, string sourceApplication, NSObject annotation)
	    {
	        //_app.Import(url.Query);
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

