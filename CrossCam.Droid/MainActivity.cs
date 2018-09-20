using Android;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Support.V4.App;
using Android.Support.V4.Content;
using Android.Views;
using Xamarin.Forms;

namespace CrossCam.Droid
{
    [Activity(
        Label = "CrossCam", 
        Icon = "@drawable/icon", 
        Theme = "@style/MainTheme",
        MainLauncher = true,
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation,
        ScreenOrientation = ScreenOrientation.Sensor)]
    public class MainActivity : Xamarin.Forms.Platform.Android.FormsAppCompatActivity
    {
        public OrientationHelper OrientationHelper;

        internal static MainActivity Instance { get; private set; }

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            Window.AddFlags(WindowManagerFlags.Fullscreen);
            Window.ClearFlags(WindowManagerFlags.ForceNotFullscreen);

            OrientationHelper = new OrientationHelper(this, WindowManager);
            OrientationHelper.Enable();

            Instance = this;

            Forms.Init(this, bundle);

            if (ContextCompat.CheckSelfPermission(Forms.Context, Manifest.Permission.Camera) != (int)Permission.Granted)
            {
                ActivityCompat.RequestPermissions(Instance, new[] { Manifest.Permission.Camera }, 50);
            }
            LoadApplication(new App());
        }
    }
}

