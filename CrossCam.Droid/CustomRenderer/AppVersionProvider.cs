using CrossCam.Droid.CustomRenderer;
using CrossCam.Wrappers;
using Xamarin.Forms;

[assembly: Dependency(typeof(AppVersionProvider))]
namespace CrossCam.Droid.CustomRenderer
{
    public class AppVersionProvider : IAppVersionProvider
    {
        public string GetAppVersion()
        {
            var context = MainActivity.Instance.ApplicationContext;
            var manager = context.PackageManager;
            var info = manager.GetPackageInfo(context.PackageName, 0);

            return "Android, versionName: " + info.VersionName + ", versionCode: " + info.VersionCode;
        }
    }
}