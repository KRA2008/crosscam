using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CrossCam.iOS.CustomRenderer;
using CrossCam.Wrappers;
using Xamarin.Forms;

[assembly: Dependency(typeof(Bluetooth))]
namespace CrossCam.iOS.CustomRenderer
{
    public class Bluetooth : IBluetooth
    {
        public bool IsConnected()
        {
            throw new NotImplementedException();
        }

        public Task<bool> RequestBluetoothPermissions()
        {
            throw new NotImplementedException();
        }

        public Task<bool> RequestLocationPermissions()
        {
            throw new NotImplementedException();
        }

        public bool IsBluetoothSupported()
        {
            throw new NotImplementedException();
        }

        public Task<bool> TurnOnBluetooth()
        {
            throw new NotImplementedException();
        }

        public Task<bool> TurnOnLocationServices()
        {
            throw new NotImplementedException();
        }

        public List<PartnerDevice> GetPairedDevices()
        {
            throw new NotImplementedException();
        }

        public bool BeginSearchForDiscoverableDevices()
        {
            throw new NotImplementedException();
        }

        public event EventHandler<PartnerDevice> DeviceDiscovered;
        public Task<bool> BecomeDiscoverable()
        {
            throw new NotImplementedException();
        }

        public Task<bool> ListenForConnections()
        {
            throw new NotImplementedException();
        }

        public Task<bool> AttemptConnection(PartnerDevice partnerDevice)
        {
            throw new NotImplementedException();
        }

        public void ForgetDevice(PartnerDevice partnerDevice)
        {
            throw new NotImplementedException();
        }
    }
}