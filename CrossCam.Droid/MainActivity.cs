using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Support.V4.App;
using Android.Support.V4.Content;
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
        ScreenOrientation = ScreenOrientation.FullUser)]
    public class MainActivity : Xamarin.Forms.Platform.Android.FormsAppCompatActivity
    {
        public LifecycleEventListener LifecycleEventListener;

        internal static MainActivity Instance { get; private set; }

        private const int CAMERA_PERMISSION_REQUEST_CODE = 50;
        private const int WRITE_TO_STORAGE_REQUEST_CODE = 51;

        public const int PICK_PHOTO_ID = 1000;
        public TaskCompletionSource<byte[]> PickPhotoTaskCompletionSource { set; get; }

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);
            Xamarin.Essentials.Platform.Init(this, bundle);

            Window.AddFlags(WindowManagerFlags.Fullscreen);
            Window.ClearFlags(WindowManagerFlags.ForceNotFullscreen);

            LifecycleEventListener = new LifecycleEventListener(this, WindowManager);
            LifecycleEventListener.Enable();

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
        
        protected override void OnPause()
        {
            base.OnPause();
            LifecycleEventListener.OnAppMinimized();
        }

        protected override void OnResume()
        {
            base.OnResume();
            LifecycleEventListener.OnAppMaximized();
        }    

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent intent)
        {
            base.OnActivityResult(requestCode, resultCode, intent);

            if (requestCode == PICK_PHOTO_ID)
            {
                if (resultCode == Result.Ok && 
                    intent != null)
                {
                    var uri = intent.Data;
                    var stream = ContentResolver.OpenInputStream(uri);
                    var memoryStream = new MemoryStream();
                    stream.CopyTo(memoryStream);

                    // Set the Stream as the completion of the Task
                    PickPhotoTaskCompletionSource.SetResult(memoryStream.ToArray());
                }
                else
                {
                    PickPhotoTaskCompletionSource.SetResult(null);
                }
            }
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
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
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }
}