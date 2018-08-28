using Foundation;
using UIKit;
using Xamarin.Forms;

namespace CustomRenderer.iOS
{
	[Register ("AppDelegate")]
	public partial class AppDelegate : Xamarin.Forms.Platform.iOS.FormsApplicationDelegate
	{
		public override bool FinishedLaunching (UIApplication app, NSDictionary options)
		{
			Forms.Init();

            LoadApplication(new App());

		    NSNotificationCenter.DefaultCenter.AddObserver(new NSString("UIDeviceOrientationDidChangeNotification"), DeviceRotated);

		    return base.FinishedLaunching(app, options);
        }

	    private void DeviceRotated(NSNotification notification)
	    {
	        switch (UIDevice.CurrentDevice.Orientation)
	        {
	            case UIDeviceOrientation.LandscapeRight:
	                MessagingCenter.Send(this, "orientationChanged");
	                break;
	            case UIDeviceOrientation.LandscapeLeft:
	                MessagingCenter.Send(this, "orientationChanged");
	                break;
	        }
	    }
    }
}

