using Android.Bluetooth;
using CrossCam.Droid.CustomRenderer;
using CrossCam.Wrappers;
using Xamarin.Forms;

[assembly: Dependency(typeof(Bluetooth))]
namespace CrossCam.Droid.CustomRenderer
{
    public class Bluetooth : IBluetooth
    {
        public void What()
        {
            var adapter = BluetoothAdapter.DefaultAdapter;
            var devices = adapter.BondedDevices;
            var isEnabled = adapter.IsEnabled;
            var didStart = adapter.StartDiscovery();
        }
    }
}