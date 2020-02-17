﻿using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossCam.CustomElement;
using CrossCam.Wrappers;
using FreshMvvm;
using Xamarin.Forms;

namespace CrossCam.ViewModel
{
    public class PairingViewModel : FreshBasePageModel
    {
        public BluetoothOperator BluetoothOperator => CameraViewModel.BluetoothOperator;
        public ObservableCollection<PartnerDevice> PairedDevices { get; set; }
        public ObservableCollection<PartnerDevice> DiscoveredDevices { get; set; }

        public Command DisconnectCommand { get; set; }
        public Command InitialSetupCommand { get; set; }
        public Command InitialSetupiOSPart2Command { get; set; }
        public Command PairedSetupCommand { get; set; }
        public Command AttemptConnectionCommand { get; set; }

        private int _initializeThreadLocker;
        private int _pairInitializeThreadLocker;
        private int _connectThreadLocker;

        public PairingViewModel()
        {
            DiscoveredDevices = new ObservableCollection<PartnerDevice>();
            PairedDevices = new ObservableCollection<PartnerDevice>();

            DisconnectCommand = new Command(() =>
            {
                CameraViewModel.BluetoothOperator.Disconnect();
            });

            InitialSetupCommand = new Command(async () =>
            {
                if (Interlocked.CompareExchange(ref _initializeThreadLocker, 1, 0) == 0)
                {
                    try
                    {
                        if (Device.RuntimePlatform == Device.Android)
                        {
                            DiscoveredDevices.Clear();
                            await CameraViewModel.BluetoothOperator.InitializeForPairingAndroid();
                        }
                        else
                        {
                            await CameraViewModel.BluetoothOperator.InitializeForPairingiOSA();
                        }
                    }
                    catch (Exception e)
                    {
                        await HandleBluetoothException(e);
                    }
                    finally
                    {
                        _initializeThreadLocker = 0;
                    }
                }
            });

            InitialSetupiOSPart2Command = new Command(async () =>
            {
                if (Interlocked.CompareExchange(ref _initializeThreadLocker, 1, 0) == 0)
                {
                    try
                    {
                        await CameraViewModel.BluetoothOperator.InitializeForPairingiOSB();
                    }
                    catch (Exception e)
                    {
                        await HandleBluetoothException(e);
                    }
                    finally
                    {
                        _initializeThreadLocker = 0;
                    }
                }
            });

            PairedSetupCommand = new Command(async obj =>
            {
                if (Interlocked.CompareExchange(ref _pairInitializeThreadLocker, 1, 0) == 0)
                {
                    try
                    {
                        await CameraViewModel.BluetoothOperator.InitializeForPairedConnection();
                        PairedDevices = new ObservableCollection<PartnerDevice>(CameraViewModel.BluetoothOperator.GetPairedDevices());
                        CameraViewModel.BluetoothOperator.GetPairedDevices();
                    }
                    catch (Exception e)
                    {
                        await HandleBluetoothException(e);
                    }
                    finally
                    {
                        _pairInitializeThreadLocker = 0;
                    }
                }
            });

            AttemptConnectionCommand = new Command(async obj =>
            {
                if (Interlocked.CompareExchange(ref _connectThreadLocker, 1, 0) == 0)
                {
                    try
                    {
                        CameraViewModel.BluetoothOperator.Connect((PartnerDevice) obj);
                    }
                    catch (Exception e)
                    {
                        await HandleBluetoothException(e);
                    }
                    finally
                    {
                        _connectThreadLocker = 0;
                    }
                }
            });
        }

        private async Task HandleBluetoothException(Exception e)
        {
            await Device.InvokeOnMainThreadAsync(async () => 
            {
                switch (e)
                {
                    case PermissionsException _:
                        await CoreMethods.DisplayAlert("Permissions Denied",
                            "The necessary permissions for pairing were not granted. Exception: " + e, "OK");
                        break;
                    case BluetoothNotSupportedException _:
                        await CoreMethods.DisplayAlert("Bluetooth Not Supported",
                            "Bluetooth is not supported on this device. Exception: " + e, "OK");
                        break;
                    case BluetoothFailedToSearchException _:
                        await CoreMethods.DisplayAlert("Failed to Search",
                            "The device failed to search for devices. Exception: " + e, "OK");
                        break;
                    case BluetoothNotTurnedOnException _:
                        await CoreMethods.DisplayAlert("Bluetooth Not On",
                            "The device failed to power on Bluetooth. Exception: " + e, "OK");
                        break;
                    default:
                        await CoreMethods.DisplayAlert("Error",
                            "An error occurred. Exception: " + e, "OK");
                        break;
                }
            });
        }

        protected override void ViewIsAppearing(object sender, EventArgs e)
        {
            base.ViewIsAppearing(sender, e);

            CameraViewModel.BluetoothOperator.CurrentCoreMethods = CoreMethods;

            CameraViewModel.BluetoothOperator.GetPairedDevices();

            CameraViewModel.BluetoothOperator.Connected += OperatorOnConnected;
            CameraViewModel.BluetoothOperator.Disconnected += OperatorOnDisconnected;
            CameraViewModel.BluetoothOperator.DeviceDiscovered += OperatorOnDeviceDiscovered;
        }

        private void OperatorOnDeviceDiscovered(object sender, PartnerDevice e)
        {
            if (DiscoveredDevices.All(d => d.Address != e.Address))
            {
                Device.BeginInvokeOnMainThread(() =>
                {
                    DiscoveredDevices.Add(e);
                });
            }
        }

        private static void OperatorOnDisconnected(object sender, EventArgs e)
        {
            CameraViewModel.BluetoothOperator.GetPairedDevices();
        }

        private static void OperatorOnConnected(object sender, EventArgs e)
        {
            CameraViewModel.BluetoothOperator.GetPairedDevices();
        }

        protected override void ViewIsDisappearing(object sender, EventArgs e)
        {
            CameraViewModel.BluetoothOperator.Connected -= OperatorOnConnected;
            CameraViewModel.BluetoothOperator.Disconnected -= OperatorOnDisconnected;
            CameraViewModel.BluetoothOperator.DeviceDiscovered -= OperatorOnDeviceDiscovered;

            //TODO: be sure scanning and advertising are turned off

            base.ViewIsDisappearing(sender, e);
        }
    }
}