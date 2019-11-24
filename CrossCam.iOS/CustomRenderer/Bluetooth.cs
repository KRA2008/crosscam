﻿using System;
using System.Threading.Tasks;
using CrossCam.iOS.CustomRenderer;
using CrossCam.Wrappers;
using Xamarin.Forms;

[assembly: Dependency(typeof(Bluetooth))]
namespace CrossCam.iOS.CustomRenderer
{
    public class Bluetooth : IBluetooth
    {
        public Task<bool> RequestBluetoothPermissions()
        {
            return Task.FromResult(true);
        }

        public Task<bool> RequestLocationPermissions()
        {
            return Task.FromResult(true);
        }

        public bool IsBluetoothSupported()
        {
            return true;
        }

        public bool IsServerSupported()
        {
            return true;
        }

        public Task<bool> TurnOnBluetooth()
        {
            return Task.FromResult(true);
        }

        public Task<bool> TurnOnLocationServices()
        {
            return Task.FromResult(true);
        }

        public Task<bool> SendPreviewFrame(byte[] preview)
        {
            throw new NotImplementedException();
        }

        public Task<byte[]> Capture(int countdownSeconds)
        {
            throw new NotImplementedException();
        }
    }
}