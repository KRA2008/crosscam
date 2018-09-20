using System.Linq;
using Android;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Support.V4.App;
using Android.Support.V4.Content;
using Android.Support.V7.Widget;
using Android.Views;
using Java.Lang;
using Xamarin.Forms;

namespace CrossCam.Droid
{
    [Activity(
        Label = "CrossCam", 
        Icon = "@drawable/icon", 
        Theme = "@style/MainTheme",
        MainLauncher = false,
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation,
        ScreenOrientation = ScreenOrientation.Sensor)]
    public class MainActivity : Xamarin.Forms.Platform.Android.FormsAppCompatActivity
    {
        public OrientationHelper OrientationHelper;

        internal static MainActivity Instance { get; private set; }

        private const int CAMERA_PERMISSION_REQUEST_CODE = 50;
        private const int WRITE_TO_STORAGE_REQUEST_CODE = 51;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            Window.AddFlags(WindowManagerFlags.Fullscreen);
            Window.ClearFlags(WindowManagerFlags.ForceNotFullscreen);

            OrientationHelper = new OrientationHelper(this, WindowManager);
            OrientationHelper.Enable();

            Instance = this;

            Forms.Init(this, bundle);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                if (ContextCompat.CheckSelfPermission(Forms.Context, Manifest.Permission.Camera) != (int)Permission.Granted)
                {
                    ActivityCompat.RequestPermissions(Instance, new[] { Manifest.Permission.Camera }, CAMERA_PERMISSION_REQUEST_CODE);
                }
                else
                {
                    LoadApplication(new App());
                }
            }
            else
            {
                LoadApplication(new App());
            }
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            if (requestCode == CAMERA_PERMISSION_REQUEST_CODE)
            {
                if (grantResults.Contains(Permission.Granted))
                {
                    if (ContextCompat.CheckSelfPermission(Forms.Context, Manifest.Permission.WriteExternalStorage) !=
                        (int) Permission.Granted)
                    {
                        ActivityCompat.RequestPermissions(Instance, new[] {Manifest.Permission.WriteExternalStorage},
                            WRITE_TO_STORAGE_REQUEST_CODE);
                    }
                    else
                    {
                        LoadApplication(new App());
                    }
                }
                else
                {
                    JavaSystem.Exit(0);
                }
            }
            else if (requestCode == WRITE_TO_STORAGE_REQUEST_CODE)
            {
                if (grantResults.Contains(Permission.Granted))
                {
                    LoadApplication(new App());
                }
                else
                {
                    JavaSystem.Exit(0);
                }
            }
        }
    }
}