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

		    return base.FinishedLaunching(app, options);
        }
    }
}

