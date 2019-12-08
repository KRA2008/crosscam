using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossCam.Wrappers;
using FreshMvvm;
using Plugin.BluetoothLE;
using Xamarin.Forms;
using IDevice = Plugin.BluetoothLE.IDevice;

namespace CrossCam.ViewModel
{
    public class PairingViewModel : FreshBasePageModel
    {
        public BluetoothOperator BluetoothOperator => CameraViewModel.BluetoothOperator;
        public ObservableCollection<IDevice> PairedDevices { get; set; }
        public ObservableCollection<IDevice> DiscoveredDevices { get; set; }

        public Command DisconnectCommand { get; set; }
        public Command InitialSetupCommand { get; set; }
        public Command PairedSetupCommand { get; set; }
        public Command AttemptConnectionCommand { get; set; }

        private int _initializeThreadLocker;
        private int _pairInitializeThreadLocker;
        private int _connectThreadLocker;

        public PairingViewModel()
        {
            DiscoveredDevices = new ObservableCollection<IDevice>();
            PairedDevices = new ObservableCollection<IDevice>();

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
                        DiscoveredDevices.Clear();
                        await CameraViewModel.BluetoothOperator.InitializeForPairing();
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
                        CameraViewModel.BluetoothOperator.Connect((IDevice) obj);
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

        private async Task ShowConnectionSucceeded()
        {
            CrossBleAdapter.Current.Advertiser.Stop();
            CrossBleAdapter.Current.StopScan();
            CameraViewModel.BluetoothOperator.GetPairedDevices();
            await Device.InvokeOnMainThreadAsync(async () =>
            {
                await CoreMethods.DisplayAlert("Connection Success", "Congrats!", "Yay");
            });
        }

        private async Task ShowDisconnected()
        {
            await Device.InvokeOnMainThreadAsync(async () =>
            {
                await CoreMethods.DisplayAlert("Disconnected", "Disconnected!", "OK");
            });
        }

        protected override void ViewIsAppearing(object sender, EventArgs e)
        {
            base.ViewIsAppearing(sender, e);
            CameraViewModel.BluetoothOperator.GetPairedDevices();

            CameraViewModel.BluetoothOperator.ErrorOccurred += OperatorOnErrorOccurred;
            CameraViewModel.BluetoothOperator.Connected += OperatorOnConnected;
            CameraViewModel.BluetoothOperator.Disconnected += OperatorOnDisconnected;
            CameraViewModel.BluetoothOperator.DeviceDiscovered += OperatorOnDeviceDiscovered;
            CameraViewModel.BluetoothOperator.PairedDevicesFound += OperatorOnPairedDevicesFound;
        }

        private void OperatorOnPairedDevicesFound(object sender, PairedDevicesFoundEventArgs e)
        {
            PairedDevices = new ObservableCollection<IDevice>(e.Devices);
        }

        private void OperatorOnDeviceDiscovered(object sender, BluetoothDeviceDiscoveredEventArgs e)
        {
            if (DiscoveredDevices.All(d => d.Uuid != e.Device.Uuid))
            {
                DiscoveredDevices.Add(e.Device);
            }
        }

        private async void OperatorOnDisconnected(object sender, EventArgs e)
        {
            await ShowDisconnected();
        }

        private async void OperatorOnConnected(object sender, EventArgs e)
        {
            await ShowConnectionSucceeded();
        }

        private async void OperatorOnErrorOccurred(object sender, ErrorEventArgs e)
        {
            await Device.InvokeOnMainThreadAsync(async () =>
            {
                await CoreMethods.DisplayAlert("Error Occurred",
                    "An error occurred during " + e.Step + ", exception: " + e.Exception, "OK");
            });
        }

        protected override void ViewIsDisappearing(object sender, EventArgs e)
        {
            CrossBleAdapter.Current.StopScan();
            CrossBleAdapter.Current.Advertiser.Stop();

            CameraViewModel.BluetoothOperator.ErrorOccurred -= OperatorOnErrorOccurred;
            CameraViewModel.BluetoothOperator.Connected -= OperatorOnConnected;
            CameraViewModel.BluetoothOperator.Disconnected -= OperatorOnDisconnected;
            CameraViewModel.BluetoothOperator.DeviceDiscovered -= OperatorOnDeviceDiscovered;
            CameraViewModel.BluetoothOperator.PairedDevicesFound -= OperatorOnPairedDevicesFound;

            base.ViewIsDisappearing(sender, e);
        }
    }
}