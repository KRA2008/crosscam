using CrossCam.Droid.CustomRenderer;
using CrossCam.Wrappers;
using Xamarin.Forms;

[assembly: Dependency(typeof(Bluetooth))]
namespace CrossCam.Droid.CustomRenderer
{
    public class Bluetooth : IBluetooth
    {
        public async void What()
        {
        }
    }
}