using Android;
using Android.App;
using Android.Bluetooth;
using Android.Content;
using Android.Content.PM;
using Android.Gms.Tasks;
using Android.OS;
using Android.Views;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using CrossCam.Platforms.Android.CustomRenderer;
using Java.Lang;
#if !DEBUG
using Microsoft.AppCenter;
#endif
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Xamarin.Google.Android.Play.Core.Review;
using Xamarin.Google.Android.Play.Core.Review.Testing;
using Debug = System.Diagnostics.Debug;
using Task = System.Threading.Tasks.Task;
using Uri = Android.Net.Uri;
using AndroidGms = Android.Gms;
using Microsoft.Maui.Controls.Compatibility.Platform.Android;

namespace CrossCam.Platforms.Android
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
    public class MainActivity : MauiAppCompatActivity, IOnCompleteListener
    {
        public LifecycleEventListener LifecycleEventListener;

        internal static MainActivity Instance { get; private set; }

        private App _app;
        private TaskCompletionSource<bool> _requestReviewTaskCompletionSource;
        private bool _requestReviewCheckComplete;
        private IReviewManager _reviewManager;

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
            AccessWifiStateRequestCode,
            ChangeWifiStateRequestCode,
            NearbyWifiDevicesRequestCode,
            FineLocationRequestCode,
            CoarseLocationRequestCode,
            TurnLocationServicesOnRequestCode
        }

        public const int PICK_PHOTO_ID = 1000;
        public TaskCompletionSource<byte[][]> PickPhotoTaskCompletionSource { set; get; }

        public MainActivity()
        {
            Instance = this;
        }

        protected override void OnCreate(Bundle bundle)
        {
#if !DEBUG
            AppCenter.Start("febfa1c4-10aa-4087-9594-71d287579841", // plz don't abuse this.
                typeof(Analytics), typeof(Crashes));
#endif
            base.OnCreate(bundle);

            DeviceDisplay.MainDisplayInfoChanged += SetFullscreen; 
            SetFullscreen(null, null);

            LifecycleEventListener = new LifecycleEventListener(this, WindowManager);
            LifecycleEventListener.Enable();
            
            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                if (CheckForAndRequestBasicRequiredPermissions())
                {
                    return;
                }
            }
        }

        protected override void OnPause()
        {
            Window?.ClearFlags(WindowManagerFlags.KeepScreenOn);
            LifecycleEventListener.OnAppMinimized();
            base.OnPause();
        }

        protected override async void OnResume()
        {
            base.OnResume(); 
            LifecycleEventListener.OnAppMaximized();
            Window?.AddFlags(WindowManagerFlags.KeepScreenOn);

            if (Intent.ActionSend.Equals(Intent?.Action) && 
                Intent.Type != null &&
                Intent.Type.StartsWith("image/") &&
                Intent.GetParcelableExtra(Intent.ExtraStream) is Uri uri)
            {
                var image = await ImageUriToByteArray(uri);
                _app.LoadSharedImages(image, null);
            }
            else if (Intent.ActionSendMultiple.Equals(Intent?.Action) && 
                     Intent.Type != null &&
                     Intent.Type.StartsWith("image/") &&
                     Intent.GetParcelableArrayListExtra(Intent.ExtraStream) is {} parcelables)
            {
                if (parcelables[0] is Uri uri1 &&
                    parcelables[1] is Uri uri2)
                {
                    var image1Task = ImageUriToByteArray(uri1);
                    var image2Task = ImageUriToByteArray(uri2);
                    await Task.WhenAll(image1Task, image2Task);
                    _app.LoadSharedImages(image1Task.Result, image2Task.Result);
                }
            }

            Intent?.RemoveExtra(Intent.ExtraStream);
        }    

        protected override async void OnActivityResult(int requestCode, Result resultCode, Intent intent)
        {
            try
            {
                base.OnActivityResult(requestCode, resultCode, intent);

                if (requestCode == PICK_PHOTO_ID)
                {
                    if (intent?.ClipData != null &&
                        intent.ClipData.ItemCount > 1 &&
                        intent.ClipData.GetItemAt(0) is { } item1 &&
                        intent.ClipData.GetItemAt(1) is { } item2)
                    {
                        var image1Task = ImageUriToByteArray(item1.Uri);
                        var image2Task = ImageUriToByteArray(item2.Uri);
                        await Task.WhenAll(image1Task, image2Task);

                        if (image1Task.Status == TaskStatus.RanToCompletion &&
                            image2Task.Status == TaskStatus.RanToCompletion)
                        {
                            PickPhotoTaskCompletionSource?.SetResult(new[] { image1Task.Result, image2Task.Result });
                        }
                        else
                        {
                            Crashes.TrackError(image1Task.Exception);
                            Crashes.TrackError(image2Task.Exception);
                            PickPhotoTaskCompletionSource?.SetResult(null);
                        }
                    }
                    else if (resultCode == Result.Ok &&
                             intent?.Data != null &&
                             ContentResolver != null)
                    {
                        var uri = intent.Data;
                        var stream = ContentResolver.OpenInputStream(uri);
                        var memoryStream = new MemoryStream();
                        if (stream != null) await stream.CopyToAsync(memoryStream);

                        PickPhotoTaskCompletionSource?.SetResult(new[] {memoryStream.ToArray(), null});
                    }
                    else
                    {
                        PickPhotoTaskCompletionSource?.SetResult(null);
                        if (resultCode != Result.Canceled)
                        {
                            Crashes.TrackError(new System.Exception("pickPhotoCompletion failed to enter into photo opening"), new Dictionary<string, string>()
                            {
                                {"resultCode",resultCode.ToString()},
                                {"intent.Data",intent?.Data?.ToString()},
                                {"contentResolver",ContentResolver?.ToString()}
                            });
                        }
                    }
                }
                else if (requestCode == (int) RequestCodes.BrowseDirectoriesRequestCode)
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
                else if (requestCode == (int)RequestCodes.TurnLocationServicesOnRequestCode)
                {

                    await PlatformPair.CheckForAndTurnOnLocationServices(true);
                }
            }
            catch (System.Exception ex)
            {
                PickPhotoTaskCompletionSource?.SetResult(null);
                Crashes.TrackError(ex);
            }
        }

        private void SetFullscreen(object sender, DisplayInfoChangedEventArgs e)
        {
            if (Window != null)
            {
                if (DeviceDisplay.MainDisplayInfo.Orientation == DisplayOrientation.Landscape)
                {
                    Window.SetStatusBarColor(Microsoft.Maui.Graphics.Colors.Transparent.ToAndroid());
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
                    Window.SetStatusBarColor(Microsoft.Maui.Graphics.Colors.Black.ToAndroid());
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
            if (requestCode == (int)RequestCodes.CameraPermissionRequestCode ||
                requestCode == (int)RequestCodes.WriteToStorageRequestCode)
            {
                if (!grantResults.Contains(Permission.Granted))
                {
                    if (requestCode == (int)RequestCodes.CameraPermissionRequestCode)
                    {
                        Crashes.TrackError(new System.Exception("Camera permission not granted."));
                    } 
                    else if (requestCode == (int)RequestCodes.WriteToStorageRequestCode)
                    {
                        Crashes.TrackError(new System.Exception("Saving permission not granted."));
                    }

                    var builder = new AlertDialog.Builder(this);
                    builder.SetTitle("Required permissions not granted");
                    builder.SetMessage(
                        "CrossCam requires permission to use the camera and to save images in order to work. Because either of these were not granted, CrossCam will now exit.");
                    builder.SetNegativeButton("OK", (sender, args) =>
                    {
                        JavaSystem.Exit(0);
                    });
                    builder.Show();
                    return;
                }
            }

            if (requestCode == (int) RequestCodes.BluetoothBasicRequestCode ||
                requestCode == (int) RequestCodes.BluetoothAdminRequestCode ||
                requestCode == (int) RequestCodes.BluetoothAdvertiseRequestCode ||
                requestCode == (int) RequestCodes.BluetoothScanRequestCode ||
                requestCode == (int) RequestCodes.BluetoothConnectRequestCode ||
                requestCode == (int) RequestCodes.NearbyWifiDevicesRequestCode ||
                requestCode == (int) RequestCodes.AccessWifiStateRequestCode ||
                requestCode == (int) RequestCodes.ChangeWifiStateRequestCode)
            {
                if (!grantResults.Contains(Permission.Granted))
                {
                    PlatformPair.ConnectionsPermissionsTask.SetResult(false);
                    return;
                }

                CheckForAndRequestConnectionsPermissions();
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

            if (CheckForAndRequestBasicRequiredPermissions())
            {
                return;
            }

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        public void CheckForAndRequestLocationPermissions()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q &&
                Build.VERSION.SdkInt <= BuildVersionCodes.S &&
                ContextCompat.CheckSelfPermission(this, Manifest.Permission.AccessFineLocation) !=
                (int)Permission.Granted)
            {
                ActivityCompat.RequestPermissions(Instance, new[] { Manifest.Permission.AccessFineLocation },
                    (int)RequestCodes.FineLocationRequestCode);
                return;
            }

            if (Build.VERSION.SdkInt <= BuildVersionCodes.P &&
                ContextCompat.CheckSelfPermission(this, Manifest.Permission.AccessCoarseLocation) !=
                (int)Permission.Granted)
            {
                ActivityCompat.RequestPermissions(Instance, new[] { Manifest.Permission.AccessCoarseLocation },
                    (int)RequestCodes.CoarseLocationRequestCode);
                return;
            }

            PlatformPair.LocationPermissionsTask.SetResult(true);
        }

        public void CheckForAndRequestConnectionsPermissions()
        {
            if (Build.VERSION.SdkInt <= BuildVersionCodes.R &&
                ContextCompat.CheckSelfPermission(this, Manifest.Permission.Bluetooth) != 
                (int)Permission.Granted)
            {
                ActivityCompat.RequestPermissions(Instance, new[] { Manifest.Permission.Bluetooth },
                    (int)RequestCodes.BluetoothBasicRequestCode);
                return;
            }

            if (Build.VERSION.SdkInt <= BuildVersionCodes.R &&
                ContextCompat.CheckSelfPermission(this, Manifest.Permission.BluetoothAdmin) !=
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

            if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.AccessWifiState) !=
                (int)Permission.Granted)
            {
                ActivityCompat.RequestPermissions(Instance, new[] { Manifest.Permission.AccessWifiState },
                    (int)RequestCodes.AccessWifiStateRequestCode);
                return;
            }

            if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.ChangeWifiState) !=
                (int)Permission.Granted)
            {
                ActivityCompat.RequestPermissions(Instance, new[] { Manifest.Permission.ChangeWifiState },
                    (int)RequestCodes.AccessWifiStateRequestCode);
                return;
            }

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu &&
                ContextCompat.CheckSelfPermission(this, Manifest.Permission.NearbyWifiDevices) !=
                (int)Permission.Granted)
            {
                ActivityCompat.RequestPermissions(Instance, new[] { Manifest.Permission.NearbyWifiDevices },
                    (int)RequestCodes.NearbyWifiDevicesRequestCode);
                return;
            }

            PlatformPair.ConnectionsPermissionsTask.SetResult(true);
        }

        private bool CheckForAndRequestBasicRequiredPermissions()
        {
            try
            {
                if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.Camera) != 
                    (int) Permission.Granted)
                {
                    ActivityCompat.RequestPermissions(Instance, new[] {Manifest.Permission.Camera},
                        (int) RequestCodes.CameraPermissionRequestCode);

                    return true;
                }

                if (Build.VERSION.SdkInt < BuildVersionCodes.Q &&
                    ContextCompat.CheckSelfPermission(this, Manifest.Permission.WriteExternalStorage) !=
                    (int) Permission.Granted)
                {
                    ActivityCompat.RequestPermissions(Instance, new[] {Manifest.Permission.WriteExternalStorage},
                        (int) RequestCodes.WriteToStorageRequestCode);

                    return true;
                }

                return false;
            }
            catch (System.Exception e)
            {
                Crashes.TrackError(e);

                return true;
            }
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
                var imageStream = ContentResolver?.OpenInputStream(uri);
                var imageMemStream = new MemoryStream();
                imageStream?.CopyTo(imageMemStream);
                return imageMemStream.ToArray();
            });
        }

        public void RequestReview(TaskCompletionSource<bool> requestReviewTaskCompletionSource)
        {
            _requestReviewTaskCompletionSource = requestReviewTaskCompletionSource;
            _requestReviewCheckComplete = false;
#if DEBUG
            _reviewManager ??= new FakeReviewManager(this);
#else
            _reviewManager ??= ReviewManagerFactory.Create(this);
#endif
            var request = _reviewManager.RequestReviewFlow();
            request.AddOnCompleteListener(this);
        }

        public void OnComplete(AndroidGms.Tasks.Task task)
        {
            if (task.IsSuccessful && _requestReviewCheckComplete)
            {
                _requestReviewTaskCompletionSource.SetResult(true);
                return;
            }
            
            if (!task.IsSuccessful)
            {
                _requestReviewTaskCompletionSource.SetResult(false);
            }

            try
            {
                var reviewInfo = (ReviewInfo)task.GetResult(Class.FromType(typeof(ReviewInfo)));
                _requestReviewCheckComplete = true;
                var launchTask = _reviewManager.LaunchReviewFlow(this, reviewInfo);
                launchTask.AddOnCompleteListener(this);
            }
            catch (System.Exception ex)
            {
                Crashes.TrackError(ex);
                _requestReviewTaskCompletionSource.TrySetResult(false);
            }
        }

        public override void OnLowMemory()
        {
            base.OnLowMemory();
            Debug.WriteLine("### LOW MEMORY! OH NO!");
            Analytics.TrackEvent("low memory");
        }
    }
}