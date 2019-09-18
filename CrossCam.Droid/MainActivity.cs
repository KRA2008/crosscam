using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Android;
using Android.App;
using Android.Bluetooth;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Support.V4.App;
using Android.Support.V4.Content;
using Android.Support.V4.OS;
using Android.Support.V4.Provider;
using Android.Views;
using CrossCam.Droid.CustomRenderer;
using Java.Lang;
using Xamarin.Forms;
using Debug = System.Diagnostics.Debug;
using Uri = Android.Net.Uri;

namespace CrossCam.Droid
{
    [Activity(
        Label = "CrossCam", 
        Icon = "@drawable/icon", 
        Theme = "@style/MainTheme",
        MainLauncher = false,
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation,
        ScreenOrientation = ScreenOrientation.FullUser,
        LaunchMode = LaunchMode.SingleTop)]
    [IntentFilter(
        new[] {Intent.ActionSend,Intent.ActionSendMultiple}, 
        Categories = new[] {Intent.CategoryDefault},
        DataMimeType = "image/*", 
        Icon = "@drawable/icon")]
    [IntentFilter(
        new [] {BluetoothDevice.ActionFound})]
    public class MainActivity : Xamarin.Forms.Platform.Android.FormsAppCompatActivity
    {
        public LifecycleEventListener LifecycleEventListener;

        internal static MainActivity Instance { get; private set; }

        private App _app;
        private BluetoothReceiver _bluetoothReceiver;

        public enum RequestCodes
        {
            CameraPermissionRequestCode,    
            WriteToStorageRequestCode,
            BrowseDirectoriesRequestCode,
            TurnOnBluetoothRequestCode,
            MakeBluetoothDiscoverableRequestCode
        }

        public const int PICK_PHOTO_ID = 1000;
        public TaskCompletionSource<byte[][]> PickPhotoTaskCompletionSource { set; get; }

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);
            _bluetoothReceiver = new BluetoothReceiver();
            Xamarin.Essentials.Platform.Init(this, bundle);

            Window.AddFlags(WindowManagerFlags.Fullscreen);
            Window.ClearFlags(WindowManagerFlags.ForceNotFullscreen);

            LifecycleEventListener = new LifecycleEventListener(this, WindowManager);
            LifecycleEventListener.Enable();

            Instance = this;

            Forms.Init(this, bundle);
            
            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                if (CheckForAndRequestPermissions())
                {
                    return;
                }
            }

            _app = new App();
            LoadApplication(_app);
        }
        
        protected override void OnPause()
        {
            UnregisterReceiver(_bluetoothReceiver);
            LifecycleEventListener.OnAppMinimized();
            base.OnPause();
        }

        protected override async void OnResume()
        {
            base.OnResume();
            RegisterReceiver(_bluetoothReceiver, new IntentFilter(BluetoothDevice.ActionFound));
            RegisterReceiver(_bluetoothReceiver, new IntentFilter(BluetoothDevice.ActionAclConnected));
            RegisterReceiver(_bluetoothReceiver, new IntentFilter(BluetoothDevice.ActionAclDisconnected));
            RegisterReceiver(_bluetoothReceiver, new IntentFilter(BluetoothDevice.ActionAclDisconnectRequested));
            LifecycleEventListener.OnAppMaximized();

            if (Intent.ActionSend.Equals(Intent.Action) && 
                Intent.Type != null &&
                Intent.Type.StartsWith("image/") &&
                Intent.GetParcelableExtra(Intent.ExtraStream) is Uri uri)
            {
                var image = await ImageUriToByteArray(uri);
                _app.LoadSharedImages(image, null);
            }
            else if (Intent.ActionSendMultiple.Equals(Intent.Action) && 
                     Intent.Type != null &&
                     Intent.Type.StartsWith("image/"))
            {
                var parcelables = Intent.GetParcelableArrayListExtra(Intent.ExtraStream);
                if (parcelables[0] is Uri uri1 &&
                    parcelables[1] is Uri uri2)
                {
                    var image1Task = ImageUriToByteArray(uri1);
                    var image2Task = ImageUriToByteArray(uri2);
                    await Task.WhenAll(image1Task, image2Task);
                    _app.LoadSharedImages(image1Task.Result, image2Task.Result);
                }
            }
        }    

        protected override async void OnActivityResult(int requestCode, Result resultCode, Intent intent)
        {
            base.OnActivityResult(requestCode, resultCode, intent);

            if (requestCode == PICK_PHOTO_ID)
            {
                if (intent?.ClipData != null &&
                    intent.ClipData.ItemCount > 1)
                {
                    var item1 = intent.ClipData.GetItemAt(0);
                    var item2 = intent.ClipData.GetItemAt(1);

                    var image1Task = ImageUriToByteArray(item1.Uri);
                    var image2Task = ImageUriToByteArray(item2.Uri);
                    await Task.WhenAll(image1Task, image2Task);

                    PickPhotoTaskCompletionSource.SetResult(new[] { image1Task.Result, image2Task.Result });
                }
                else if (resultCode == Result.Ok &&
                         intent?.Data != null)
                {
                    var uri = intent.Data;
                    var stream = ContentResolver.OpenInputStream(uri);
                    var memoryStream = new MemoryStream();
                    stream.CopyTo(memoryStream);

                    PickPhotoTaskCompletionSource.SetResult(new[] { memoryStream.ToArray(), null });
                }
                else
                {
                    PickPhotoTaskCompletionSource.SetResult(null);
                }
            }
            else if (requestCode == (int)RequestCodes.BrowseDirectoriesRequestCode)
            {
                if (resultCode == Result.Ok)
                {
                    DirectorySelector.DirectorySelected(intent.Data);
                }
                else
                {
                    DirectorySelector.DirectorySelectionCancelled();
                }
            }
            else if (requestCode == (int)RequestCodes.TurnOnBluetoothRequestCode)
            {
                Bluetooth.IsBluetoothOnSource.SetResult(resultCode == Result.Ok);
            }
            else if (requestCode == (int)RequestCodes.MakeBluetoothDiscoverableRequestCode)
            {
                Bluetooth.IsDeviceDiscoverableSource.SetResult(resultCode != Result.Canceled); // result code is discoverable duration, not pass/fail
            }
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            if (requestCode == (int)RequestCodes.CameraPermissionRequestCode)
            {
                if (!grantResults.Contains(Permission.Granted))
                {
                    JavaSystem.Exit(0);
                    return;
                }
            }

            if (requestCode == (int)RequestCodes.WriteToStorageRequestCode)
            {
                if (!grantResults.Contains(Permission.Granted))
                {
                    JavaSystem.Exit(0);
                    return;
                }
            }

            if (CheckForAndRequestPermissions())
            {
                return;
            }

            _app = new App();
            LoadApplication(_app);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        private bool CheckForAndRequestPermissions()
        {
            if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.Camera) != (int)Permission.Granted)
            {
                ActivityCompat.RequestPermissions(Instance, new[] { Manifest.Permission.Camera }, (int)RequestCodes.CameraPermissionRequestCode);
                return true;
            }

            if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.WriteExternalStorage) !=
                (int)Permission.Granted)
            {
                ActivityCompat.RequestPermissions(Instance, new[] { Manifest.Permission.WriteExternalStorage },
                    (int)RequestCodes.WriteToStorageRequestCode);
                return true;
            }

            return false;
        }

        protected override void OnNewIntent(Intent intent)
        {
            base.OnNewIntent(intent);
            Intent = intent;
        }

        private async Task<byte[]> ImageUriToByteArray(Uri uri)
        {
            return await Task.Run(() =>
            {
                var imageStream = ContentResolver.OpenInputStream(uri);
                var imageMemStream = new MemoryStream();
                imageStream.CopyTo(imageMemStream);
                return imageMemStream.ToArray();
            });
        }
    }

    [BroadcastReceiver(Enabled = true, Exported = false)]
    public class BluetoothReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context context, Intent intent)
        {
            if (BluetoothDevice.ActionFound.Equals(intent.Action))
            {
                var bluetoothDevice = (BluetoothDevice) intent.GetParcelableExtra(BluetoothDevice.ExtraDevice);
                Bluetooth.AvailableDevices.Add(bluetoothDevice);
            }
            else if (BluetoothDevice.ActionAclConnected.Equals(intent.Action))
            {
                Debug.WriteLine("Bluetooth connected!");
                //yay!
            }
            else if (BluetoothDevice.ActionAclDisconnected.Equals(intent.Action) ||
                     BluetoothDevice.ActionAclDisconnectRequested.Equals(intent.Action))
            {
                Debug.WriteLine("Bluetooth disconnected!");
                //hmmmm.
            }
        }
    }
}