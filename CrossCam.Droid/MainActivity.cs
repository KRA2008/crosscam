﻿using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Android;
using Android.App;
using Android.Bluetooth;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using CrossCam.Droid.CustomRenderer;
using Java.Lang;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;
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
        LaunchMode = LaunchMode.SingleTop,
        Exported = true)]
    [IntentFilter(
        new[] {Intent.ActionSend,Intent.ActionSendMultiple}, 
        Categories = new[] {Intent.CategoryDefault},
        DataMimeType = "image/*", 
        Icon = "@drawable/icon")]
    [IntentFilter(
        new [] {BluetoothDevice.ActionFound})]
    public class MainActivity : FormsAppCompatActivity
    {
        public LifecycleEventListener LifecycleEventListener;

        internal static MainActivity Instance { get; private set; }

        private App _app;

        public enum RequestCodes
        {
            CameraPermissionRequestCode,    
            WriteToStorageRequestCode,
            BrowseDirectoriesRequestCode,
            BluetoothBasicRequestCode,
            BluetoothAdminRequestCode,
            BluetoothScanRequestCode,
            BluetoothAdvertiseRequestCode,
            BluetoothConnectRequestCode,
            FineLocationRequestCode,
            CoarseLocationRequestCode,
            TurnLocationServicesOnRequestCode
        }

        public const int PICK_PHOTO_ID = 1000;
        public TaskCompletionSource<byte[][]> PickPhotoTaskCompletionSource { set; get; }

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);
            Xamarin.Essentials.Platform.Init(this, bundle); 
            AppCenter.Start("febfa1c4-10aa-4087-9594-71d287579841", // plz don't abuse this.
                typeof(Analytics), typeof(Crashes));

            DeviceDisplay.MainDisplayInfoChanged += SetFullscreen; 
            SetFullscreen(null, null);

            LifecycleEventListener = new LifecycleEventListener(this, WindowManager);
            LifecycleEventListener.Enable();

            Instance = this;

            Forms.SetFlags("Expander_Experimental");
            Forms.Init(this, bundle);
            
            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                if (CheckForAndRequestRequiredPermissions())
                {
                    return;
                }
            }

            _app = new App();
            LoadApplication(_app);
        }

        protected override void OnPause()
        {
            LifecycleEventListener.OnAppMinimized();
            base.OnPause();
        }

        protected override async void OnResume()
        {
            base.OnResume(); 
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
            else if (requestCode == (int) RequestCodes.TurnLocationServicesOnRequestCode)
            {

                await PlatformPair.CheckForAndTurnOnLocationServices(true);
            }
        }

        private void SetFullscreen(object sender, DisplayInfoChangedEventArgs e)
        {
            if (Window != null)
            {
                if (DeviceDisplay.MainDisplayInfo.Orientation == DisplayOrientation.Landscape)
                {
                    Window.SetStatusBarColor(Color.Transparent.ToAndroid());
                    if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
                    {
                        Window.SetDecorFitsSystemWindows(false);
                        var insetsController = Window.InsetsController;
                        insetsController?.Hide(WindowInsets.Type.NavigationBars());
                    }
                    else
                    {
                        var uiOptions = 0;
                        uiOptions |= (int)SystemUiFlags.HideNavigation;
                        uiOptions |= (int)SystemUiFlags.ImmersiveSticky;
                        uiOptions |= (int)SystemUiFlags.LayoutFullscreen;

                        Window.DecorView.SystemUiVisibility = (StatusBarVisibility)uiOptions;
                    }
                }
                else
                {
                    Window.SetStatusBarColor(Color.Black.ToAndroid());
                    if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
                    {
                        Window.SetDecorFitsSystemWindows(true);
                        var insetsController = Window.InsetsController;
                        insetsController?.Show(WindowInsets.Type.NavigationBars());
                    }
                    else
                    {
                        Window.DecorView.SystemUiVisibility = 0;
                    }
                }
            }
        }
        
        public override void OnWindowFocusChanged(bool hasFocus)
        {
            SetFullscreen(null, null);
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            if (requestCode == (int)RequestCodes.CameraPermissionRequestCode ||
                requestCode == (int)RequestCodes.WriteToStorageRequestCode)
            {
                if (!grantResults.Contains(Permission.Granted))
                {
                    JavaSystem.Exit(0);
                    return;
                }
            }

            if (requestCode == (int) RequestCodes.BluetoothBasicRequestCode ||
                requestCode == (int) RequestCodes.BluetoothAdminRequestCode ||
                requestCode == (int) RequestCodes.BluetoothAdvertiseRequestCode ||
                requestCode == (int) RequestCodes.BluetoothScanRequestCode ||
                requestCode == (int) RequestCodes.BluetoothConnectRequestCode)
            {
                if (!grantResults.Contains(Permission.Granted))
                {
                    PlatformPair.BluetoothPermissionsTask.SetResult(false);
                    return;
                }

                CheckForAndRequestBluetoothPermissions();
                return;
            }

            if (requestCode == (int)RequestCodes.FineLocationRequestCode ||
                requestCode == (int)RequestCodes.CoarseLocationRequestCode)
            {
                if (!grantResults.Contains(Permission.Granted))
                {
                    PlatformPair.LocationPermissionsTask.SetResult(false);
                    return;
                }

                CheckForAndRequestLocationPermissions();
                return;
            }

            if (CheckForAndRequestRequiredPermissions())
            {
                return;
            }

            _app = new App();
            LoadApplication(_app);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }


        public void CheckForAndRequestLocationPermissions()
        {
            if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.AccessFineLocation) !=
                (int)Permission.Granted)
            {
                ActivityCompat.RequestPermissions(Instance, new[] { Manifest.Permission.AccessFineLocation },
                    (int)RequestCodes.FineLocationRequestCode);
                return;
            }

            if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.AccessCoarseLocation) !=
                (int)Permission.Granted)
            {
                ActivityCompat.RequestPermissions(Instance, new[] { Manifest.Permission.AccessCoarseLocation },
                    (int)RequestCodes.CoarseLocationRequestCode);
                return;
            }

            PlatformPair.LocationPermissionsTask.SetResult(true);
        }

        public void CheckForAndRequestBluetoothPermissions()
        {
            if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.Bluetooth) != (int)Permission.Granted)
            {
                ActivityCompat.RequestPermissions(Instance, new[] { Manifest.Permission.Bluetooth },
                    (int)RequestCodes.BluetoothBasicRequestCode);
                return;
            }

            if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.BluetoothAdmin) !=
                (int)Permission.Granted)
            {
                ActivityCompat.RequestPermissions(Instance, new[] { Manifest.Permission.BluetoothAdmin },
                    (int)RequestCodes.BluetoothAdminRequestCode);
                return;
            }

            if (Build.VERSION.SdkInt >= BuildVersionCodes.S &&
                ContextCompat.CheckSelfPermission(this, Manifest.Permission.BluetoothAdvertise) !=
                (int)Permission.Granted)
            {
                ActivityCompat.RequestPermissions(Instance, new[] { Manifest.Permission.BluetoothAdvertise },
                    (int)RequestCodes.BluetoothAdvertiseRequestCode);
                return;
            }

            if (Build.VERSION.SdkInt >= BuildVersionCodes.S && 
                ContextCompat.CheckSelfPermission(this, Manifest.Permission.BluetoothConnect) !=
                (int)Permission.Granted)
            {
                ActivityCompat.RequestPermissions(Instance, new[] { Manifest.Permission.BluetoothConnect },
                    (int)RequestCodes.BluetoothConnectRequestCode);
                return;
            }

            if (Build.VERSION.SdkInt >= BuildVersionCodes.S && 
                ContextCompat.CheckSelfPermission(this, Manifest.Permission.BluetoothScan) !=
                (int)Permission.Granted)
            {
                ActivityCompat.RequestPermissions(Instance, new[] { Manifest.Permission.BluetoothScan },
                    (int)RequestCodes.BluetoothScanRequestCode);
                return;
            }

            PlatformPair.BluetoothPermissionsTask.SetResult(true);
        }

        private bool CheckForAndRequestRequiredPermissions()
        {
            if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.Camera) != (int)Permission.Granted)
            {
                ActivityCompat.RequestPermissions(Instance, new[] {Manifest.Permission.Camera},
                    (int) RequestCodes.CameraPermissionRequestCode);
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
}