using FFImageLoading.Forms.Touch;
using Foundation;
using UIKit;

namespace CustomRenderer.iOS
{
	[Register ("AppDelegate")]
	public partial class AppDelegate : Xamarin.Forms.Platform.iOS.FormsApplicationDelegate
	{
		public override bool FinishedLaunching (UIApplication app, NSDictionary options)
		{
			Xamarin.Forms.Forms.Init ();
		    CachedImageRenderer.Init();

            LoadApplication (new App ());

			return base.FinishedLaunching (app, options);
		}
	}
}

