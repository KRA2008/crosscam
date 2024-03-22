using Android.App;
using Android.Content;
using Android.Content.PM;

namespace CrossCam.Platforms.Android
{
    [Activity(Theme = "@style/Maui.SplashTheme", 
        ScreenOrientation = ScreenOrientation.Sensor,
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation,
        MainLauncher = true, 
        NoHistory = true)]
    public class SplashActivity : Activity
    {
        protected override void OnResume()
        {
            base.OnResume();
            StartActivity(new Intent(Application?.ApplicationContext, typeof(MainActivity)));
        }

        public override void OnBackPressed() { }
    }
}