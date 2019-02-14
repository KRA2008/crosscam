using AVFoundation;
using Foundation;
using UIKit;
using Xamarin.Forms;

namespace CrossCam.iOS
{
	[Register ("AppDelegate")]
	public partial class AppDelegate : Xamarin.Forms.Platform.iOS.FormsApplicationDelegate
	{
	    private App _app;

		public override bool FinishedLaunching (UIApplication app, NSDictionary options)
		{
			Forms.Init();

            _app = new App();
            LoadApplication(_app);
            var success = base.FinishedLaunching(app, options);
            AuthorizeCameraUse();
		    return success;
		}

	    public override bool OpenUrl(UIApplication application, NSUrl url, string sourceApplication, NSObject annotation)
	    {
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

