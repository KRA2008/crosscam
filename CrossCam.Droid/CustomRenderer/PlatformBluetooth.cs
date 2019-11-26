using System.Threading.Tasks;
using Android.Bluetooth;
using Android.Content;
using Android.Gms.Common.Apis;
using Android.Gms.Location;
using Android.OS;
using CrossCam.Droid.CustomRenderer;
using CrossCam.Wrappers;
using Xamarin.Forms;

[assembly: Dependency(typeof(PlatformBluetooth))]
namespace CrossCam.Droid.CustomRenderer
{
    public sealed class PlatformBluetooth : IPlatformBluetooth
    {
        public static TaskCompletionSource<bool> BluetoothPermissionsTask = new TaskCompletionSource<bool>();
        public static TaskCompletionSource<bool> LocationPermissionsTask = new TaskCompletionSource<bool>();
        public static TaskCompletionSource<bool> IsBluetoothOnTask = new TaskCompletionSource<bool>();

        private static TaskCompletionSource<bool> _isLocationOnTask = new TaskCompletionSource<bool>();

        public Task<bool> RequestBluetoothPermissions()
        {
            BluetoothPermissionsTask = new TaskCompletionSource<bool>();
            MainActivity.Instance.CheckForAndRequestBluetoothPermissions();
            return BluetoothPermissionsTask.Task;
        }

        public Task<bool> RequestLocationPermissions()
        {
            LocationPermissionsTask = new TaskCompletionSource<bool>();
            MainActivity.Instance.CheckForAndRequestLocationPermissions();
            return LocationPermissionsTask.Task;
        }

        public bool IsBluetoothSupported()
        {
            return BluetoothAdapter.DefaultAdapter != null;
        }

        public bool IsBluetoothApiLevelSufficient()
        {
            return Build.VERSION.SdkInt >= BuildVersionCodes.M;
        }

        public Task<bool> TurnOnBluetooth()
        {
            IsBluetoothOnTask = new TaskCompletionSource<bool>();
            if (!BluetoothAdapter.DefaultAdapter.IsEnabled)
            {
                MainActivity.Instance.StartActivityForResult(new Intent(BluetoothAdapter.ActionRequestEnable),
                    (int)MainActivity.RequestCodes.TurnOnBluetoothRequestCode);
                return IsBluetoothOnTask.Task;
            }

            return Task.FromResult(true);
        }

        public Task<bool> TurnOnLocationServices()
        {
            return CheckForAndTurnOnLocationServices();
        }

        public static Task<bool> CheckForAndTurnOnLocationServices(bool checkOnly = false)
        {
            if (!checkOnly)
            {
                _isLocationOnTask = new TaskCompletionSource<bool>();
            }

            var googleApiClient =
                new GoogleApiClient.Builder(MainActivity.Instance).AddApi(LocationServices.API).Build();
            googleApiClient.Connect();

            var builder = new LocationSettingsRequest.Builder().AddLocationRequest(new LocationRequest());
            builder.SetAlwaysShow(true);
            builder.SetNeedBle(true);

            var result = LocationServices.SettingsApi.CheckLocationSettings(googleApiClient, builder.Build());
            result.SetResultCallback((LocationSettingsResult callback) =>
            {
                switch (callback.Status.StatusCode)
                {
                    case CommonStatusCodes.Success:
                    {
                        _isLocationOnTask.SetResult(true);
                        break;
                    }
                    case CommonStatusCodes.ResolutionRequired:
                    {
                        if (!checkOnly)
                        {
                            try
                            {
                                callback.Status.StartResolutionForResult(MainActivity.Instance,
                                    (int) MainActivity.RequestCodes.TurnLocationServicesOnRequestCode);
                            }
                            catch (IntentSender.SendIntentException e)
                            {
                                _isLocationOnTask.SetResult(false);
                            }
                        }
                        else
                        {
                            _isLocationOnTask.SetResult(false);
                        }

                        break;
                    }
                    default:
                    {
                        if (!checkOnly)
                        {
                            MainActivity.Instance.StartActivity(new Intent(Android.Provider.Settings
                                .ActionLocationSourceSettings));
                        }
                        else
                        {
                            _isLocationOnTask.SetResult(false);
                        }
                        break;
                    }
                }
            });

            return _isLocationOnTask.Task;
        }
    }
}