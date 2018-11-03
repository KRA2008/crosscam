using CrossCam.iOS.CustomRenderer;
using CrossCam.Wrappers;
using Foundation;
using Xamarin.Forms;

[assembly: Dependency(typeof(AppVersionProvider))]
namespace CrossCam.iOS.CustomRenderer
{
    public class AppVersionProvider : IAppVersionProvider
    {
        public string GetAppVersion()
        {
            var bundle = NSBundle.MainBundle;
            var shortString = bundle.ObjectForInfoDictionary("CFBundleShortVersionString").ToString();
            var bundleVersion = bundle.ObjectForInfoDictionary("CFBundleVersion").ToString();
            return "iOS, versionString: " + shortString + ", version: " + bundleVersion;
        }
    }
}