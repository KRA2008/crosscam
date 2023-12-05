using Android.App;
using Android.Content;
using Android.Content.PM;

namespace CrossCam.Droid
{
    [Activity(Theme = "@style/SplashTheme", 
        ScreenOrientation = ScreenOrientation.Sensor,
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation,
        MainLauncher = true, 
        NoHistory = true)]
    public class SplashActivity : Activity
    {
        protected override void OnResume()
        {
            base.OnResume();
            StartActivity(new Intent(Application.Context, typeof(MainActivity)));
        }

        public override void OnBackPressed() { }
    }
}