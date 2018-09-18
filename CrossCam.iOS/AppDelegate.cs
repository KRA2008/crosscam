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

