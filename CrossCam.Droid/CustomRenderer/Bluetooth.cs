using System;
using System.Threading.Tasks;
using Android.Bluetooth;
using CrossCam.Droid.CustomRenderer;
using CrossCam.Wrappers;
using Java.Util;
using Xamarin.Forms;
using Application = Android.App.Application;

[assembly: Dependency(typeof(Bluetooth))]
namespace CrossCam.Droid.CustomRenderer
{
    public class Bluetooth : IBluetooth
    {
        internal static TaskCompletionSource<bool> DeviceSearchSucceeded;
        internal static BluetoothDevice BondedDevice;
        private BluetoothSocket _bluetoothSocket;
        private const string SDP_UUID = "492a8e3d-2589-40b1-b9c2-419a7ce80f3c";

        public Task<bool> SearchForABondedDevice()
        {
            var didStart = BluetoothAdapter.DefaultAdapter.StartDiscovery();
            if (didStart)
            {
                DeviceSearchSucceeded = new TaskCompletionSource<bool>();
                return DeviceSearchSucceeded.Task;
            }

            return Task.FromResult(false);
        }

        public async Task<bool> ListenForConnections()
        {
            try
            {
                var serverSocket =
                    BluetoothAdapter.DefaultAdapter.ListenUsingRfcommWithServiceRecord(Application.Context.PackageName,
                        UUID.FromString(SDP_UUID));
                _bluetoothSocket = await serverSocket.AcceptAsync();
                if (_bluetoothSocket != null)
                {
                    serverSocket.Close();
                    return _bluetoothSocket.IsConnected;
                }
            }
            catch (Exception e)
            {
                return false;
            }

            return false;
        }

        public bool AttemptConnection()
        {
            try
            {
                if (BondedDevice != null)
                {
                    _bluetoothSocket = BondedDevice.CreateRfcommSocketToServiceRecord(UUID.FromString(SDP_UUID));
                    return _bluetoothSocket.IsConnected;
                }
            }
            catch (Exception e)
            {
                return false;
            }

            return false;
        }
    }
}