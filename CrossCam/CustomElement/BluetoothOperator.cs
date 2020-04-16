using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using CrossCam.Model;
using CrossCam.Wrappers;
using FreshMvvm;
using Xamarin.Forms;
using Timer = System.Timers.Timer;

namespace CrossCam.CustomElement
{
    public sealed class BluetoothOperator : INotifyPropertyChanged
    {
        private readonly Settings _settings;
        public bool IsConnected { get; set; }
        public bool IsPrimary => _settings.IsPairedPrimary.HasValue && _settings.IsPairedPrimary.Value;
        public IPageModelCoreMethods CurrentCoreMethods { get; set; }

        private readonly IPlatformBluetooth _platformBluetooth;
        public static readonly Guid ServiceGuid = Guid.Parse("492a8e3d-2589-40b1-b9c2-419a7ce80f3c");
        public static readonly Guid ClockReadingGuid = Guid.Parse("492a8e3e-2589-40b1-b9c2-419a7ce80f3c");
        public static readonly Guid PreviewFrameGuid = Guid.Parse("492a8e3f-2589-40b1-b9c2-419a7ce80f3c");
        public static readonly Guid InitiateCaptureGuid = Guid.Parse("492a8e40-2589-40b1-b9c2-419a7ce80f3c");
        public static readonly Guid SetCaptureMomentGuid = Guid.Parse("492a8e41-2589-40b1-b9c2-419a7ce80f3c");
        public static readonly Guid CapturedImageGuid = Guid.Parse("492a8e42-2589-40b1-b9c2-419a7ce80f3c");
        public static readonly Guid HelloGuid = Guid.Parse("492a8e43-2589-40b1-b9c2-419a7ce80f3c");
        private PartnerDevice _device;
        private readonly Timer _captureSyncTimer = new Timer{AutoReset = false};

        private const int TIMER_TOTAL_SAMPLES = 25;
        private int _timerSampleIndex;
        private readonly long[] T0_SAMPLES = new long[TIMER_TOTAL_SAMPLES];
        private readonly long[] T1T2_SAMPLES = new long[TIMER_TOTAL_SAMPLES];
        private readonly long[] T3_SAMPLES = new long[TIMER_TOTAL_SAMPLES];
        private bool _isCaptureRequested;

        public ObservableCollection<PartnerDevice> PairedDevices { get; set; }
        public ObservableCollection<PartnerDevice> DiscoveredDevices { get; set; }

        public Command PairedSetupCommand { get; set; }
        public Command AttemptConnectionCommand { get; set; }

        private int _initializeThreadLocker;
        private int _pairInitializeThreadLocker;
        private int _connectThreadLocker;

        public event ElapsedEventHandler CaptureRequested;
        private void OnCaptureRequested(object sender, ElapsedEventArgs e)
        {
            Debug.WriteLine("CAPTURE NOW!!!!");
            _captureSyncTimer.Elapsed -= OnCaptureRequested;
            var handler = CaptureRequested;
            handler?.Invoke(this, e);
        }

        public event EventHandler Disconnected;
        private void OnDisconnected()
        {
            IsConnected = false;
            ShowPairDisconnected();
            GetPairedDevices();
            var handler = Disconnected;
            handler?.Invoke(this, new EventArgs());
        }

        public event EventHandler Connected;
        private async void OnConnected()
        {
            IsConnected = true;
            ShowPairConnected();
            GetPairedDevices();
            if (!IsPrimary)
            {
                await _platformBluetooth.SayHello();
            }
            var handler = Connected;
            handler?.Invoke(this, new EventArgs());
        }

        public event EventHandler<ErrorEventArgs> ErrorOccurred;
        private void OnErrorOccurred(ErrorEventArgs e)
        {
            ShowPairErrorOccurred(e.Step, e.Exception.ToString());
            var handler = ErrorOccurred;
            handler?.Invoke(this, e);
        }

        public event EventHandler<PartnerDevice> DeviceDiscovered;
        private void OnDeviceDiscovered(PartnerDevice e)
        {
            AddDiscoveredDevice(null, e);
            var handler = DeviceDiscovered;
            handler?.Invoke(this, e);
        }

        public event EventHandler PreviewFrameRequested;
        private void OnPreviewFrameRequested()
        {
            var handler = PreviewFrameRequested;
            handler?.Invoke(this, null);
        }

        public event EventHandler<byte[]> PreviewFrameReceived;
        private void OnPreviewFrameReceived(byte[] frame)
        {
            var handler = PreviewFrameReceived;
            handler?.Invoke(this, frame);
        }

        public event EventHandler<byte[]> CapturedImageReceived;
        private void OnCapturedImageReceived(byte[] frame)
        {
            var handler = CapturedImageReceived;
            handler?.Invoke(this, frame);
        }

        public BluetoothOperator(Settings settings)
        {
            _settings = settings;
            _platformBluetooth = DependencyService.Get<IPlatformBluetooth>();
            _platformBluetooth.DeviceDiscovered += PlatformBluetoothOnDeviceDiscovered;
            _platformBluetooth.Connected += PlatformBluetoothOnConnected;
            _platformBluetooth.Disconnected += PlatformBluetoothOnDisconnected;
            _platformBluetooth.PreviewFrameRequested += PlatformBluetoothOnPreviewFrameRequested;
            _platformBluetooth.PreviewFrameReceived += PlatformBluetoothOnPreviewFrameReceived;
            _platformBluetooth.ClockReadingReceived += PlatformBluetoothOnClockReadingReceived; 
            _platformBluetooth.SyncReceived += PlatformBluetoothOnSyncReceived;
            _platformBluetooth.CaptureReceived += PlatformBluetoothOnCaptureReceived;
            _platformBluetooth.HelloReceived += PlatformBluetoothOnHelloReceived;

            DiscoveredDevices = new ObservableCollection<PartnerDevice>();
            PairedDevices = new ObservableCollection<PartnerDevice>();

            PairedSetupCommand = new Command(async obj =>
            {
                if (Interlocked.CompareExchange(ref _pairInitializeThreadLocker, 1, 0) == 0)
                {
                    try
                    {
                        await InitializeForPairedConnection();
                        PairedDevices = new ObservableCollection<PartnerDevice>(GetPairedDevices());
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
                        Connect((PartnerDevice)obj);
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

        public async Task SetUpPrimaryForPairing()
        {
            if (Interlocked.CompareExchange(ref _initializeThreadLocker, 1, 0) == 0)
            {
                try
                {
                    if (Device.RuntimePlatform == Device.Android)
                    {
                        DiscoveredDevices.Clear();
                        await InitializeForPairingAndroid();
                    }
                    else
                    {
                        await InitializeForPairingiOSPrimary();
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
        }

        public async Task SetUpSecondaryForPairing()
        {
            if (Interlocked.CompareExchange(ref _initializeThreadLocker, 1, 0) == 0)
            {
                try
                {
                    await InitializeForPairingiOSSecondary();
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
        }

        private void PlatformBluetoothOnHelloReceived(object sender, EventArgs e)
        {
            RequestClockReading();
        }

        private void PlatformBluetoothOnCaptureReceived(object sender, byte[] e)
        {
            OnCapturedImageReceived(e);
        }

        private void PlatformBluetoothOnSyncReceived(object sender, DateTime e)
        {
            SyncCapture(e);
        }

        private async void PlatformBluetoothOnClockReadingReceived(object sender, long e)
        {
            T1T2_SAMPLES[_timerSampleIndex] = e;
            T3_SAMPLES[_timerSampleIndex] = DateTime.UtcNow.Ticks;
            if (_timerSampleIndex < TIMER_TOTAL_SAMPLES - 1)
            {
                _timerSampleIndex++;
            }
            else
            {
                _timerSampleIndex = 0;
            }

            if (!_isCaptureRequested)
            {
                await _platformBluetooth.SendReadyForPreviewFrame();
            }
            else
            {
                await CalculateAndApplySyncMoment();
            }
        }

        private void PlatformBluetoothOnPreviewFrameReceived(object sender, byte[] bytes)
        {
            Debug.WriteLine("Operator received frame");
            OnPreviewFrameReceived(bytes);
        }

        private void PlatformBluetoothOnPreviewFrameRequested(object sender, EventArgs e)
        {
            OnPreviewFrameRequested();
        }

        private void PlatformBluetoothOnDisconnected(object sender, EventArgs e)
        {
            OnDisconnected();
        }

        private void PlatformBluetoothOnConnected(object sender, EventArgs e)
        {
            OnConnected();
        }

        private void PlatformBluetoothOnDeviceDiscovered(object sender, PartnerDevice e)
        {
            OnDeviceDiscovered(e);
        }

        private async Task InitializeForPairingAndroid()
        {
            if (!await _platformBluetooth.RequestLocationPermissions())
            {
                throw new PermissionsException();
            }

            if (!await _platformBluetooth.TurnOnLocationServices())
            {
                throw new PermissionsException();
            }

            _platformBluetooth.StartScanning();

            await CreateGattServerAndStartAdvertisingIfCapable();

            Debug.WriteLine("### Bluetooth initialized");
        }

        private async Task InitializeForPairingiOSPrimary()
        {
            _platformBluetooth.StartScanning();
        }

        private async Task InitializeForPairingiOSSecondary()
        {
            await _platformBluetooth.BecomeDiscoverable();
        }

        private async Task InitializeForPairedConnection()
        {
            await CreateGattServerAndStartAdvertisingIfCapable();
        }

        private async Task CreateGattServerAndStartAdvertisingIfCapable()
        {
            if (!_platformBluetooth.IsBluetoothSupported())
            {
                throw new BluetoothNotSupportedException();
            }

            if (!await _platformBluetooth.RequestBluetoothPermissions())
            {
                throw new PermissionsException();
            }

            if (!await _platformBluetooth.TurnOnBluetooth())
            {
                throw new BluetoothNotTurnedOnException();
            }

            if (_platformBluetooth.IsBluetoothApiLevelSufficient() &&
               (Device.RuntimePlatform == Device.iOS ||
                _platformBluetooth.IsServerSupported()))
            {
                if (await _platformBluetooth.BecomeDiscoverable())
                {
                    await _platformBluetooth.ListenForConnections();
                }
            }
        }

        private async void Connect(PartnerDevice device)
        {
            try
            {
                await _platformBluetooth.AttemptConnection(device);
            }
            catch (Exception e)
            {
                OnErrorOccurred(new ErrorEventArgs
                {
                    Exception = e,
                    Step = "Attempt Connection"
                });
            }
        }

        public void Disconnect()
        {
            _platformBluetooth.Disconnect();
            OnDisconnected();
        }

        private IEnumerable<PartnerDevice> GetPairedDevices()
        {
            return _platformBluetooth.GetPairedDevices();
        }

        public void BeginSyncedCapture()
        {
            _isCaptureRequested = true;
        }

        public async void RequestClockReading()
        {
            T0_SAMPLES[_timerSampleIndex] = DateTime.UtcNow.Ticks;
            await _platformBluetooth.SendReadyForClockReading();
        }

        private async Task CalculateAndApplySyncMoment()
        {
            const int BUFFER_TRIP_MULTIPLIER = 4;
            var offsets = new List<long>();
            var trips = new List<long>();
            var offsetsSafeAverage = 0L;
            var tripsSafeAverage = 0L;
            try
            {
                for (var i = 0; i < TIMER_TOTAL_SAMPLES; i++)
                {
                    var t0 = T0_SAMPLES[i];
                    var t3 = T3_SAMPLES[i];
                    var t1t2 = T1T2_SAMPLES[i];

                    var timeOffsetRun = ((t1t2 - t0) + (t1t2 - t3)) / 2;
                    offsets.Add(timeOffsetRun);
                    var roundTripDelayRun = t3 - t0;
                    trips.Add(roundTripDelayRun);
                    Debug.WriteLine("Time offset: " + timeOffsetRun / 10000d + " milliseconds");
                    Debug.WriteLine("Round trip delay: " + roundTripDelayRun / 10000d + " milliseconds");
                }

                var safeOffsets = CleanseData(offsets);
                var safeTrips = CleanseData(trips);
                offsetsSafeAverage = (long)safeOffsets.Average();
                tripsSafeAverage = (long)safeTrips.Average();

                Debug.WriteLine("Average time offset: " + offsetsSafeAverage / 10000d + " milliseconds");
                Debug.WriteLine("Average round trip delay: " + tripsSafeAverage / 10000d + " milliseconds");
            }
            catch (Exception e)
            {
                OnErrorOccurred(new ErrorEventArgs
                {
                    Exception = e,
                    Step = "Send Read Time Request"
                });
            }

            var targetSyncMoment = DateTime.UtcNow.AddTicks(tripsSafeAverage * BUFFER_TRIP_MULTIPLIER);
            Debug.WriteLine("Target sync: " + targetSyncMoment.ToString("O"));
            var partnerSyncMoment = targetSyncMoment.AddTicks(offsetsSafeAverage);
            Debug.WriteLine("Partner sync: " + partnerSyncMoment.ToString("O"));
            SyncCapture(targetSyncMoment);
            await _platformBluetooth.SendSync(partnerSyncMoment);
            _isCaptureRequested = false;
        }

        private static IEnumerable<long> CleanseData(IEnumerable<long> data)
        {
            var nonZero = data.Where(d => d != 0).ToList();

            var avg = nonZero.Average();
            var sum = nonZero.Sum(d => Math.Pow(d - avg, 2));
            var stdDev = Math.Sqrt(sum / (nonZero.Count - 1));

            return nonZero.Where(datum => Math.Abs(datum - avg) < stdDev).ToArray();
        }

        private void SyncCapture(DateTime syncTime)
        {
            try
            {
                var interval = (syncTime.Ticks - DateTime.UtcNow.Ticks) / 10000d;
                Debug.WriteLine("Sync interval set: " + interval);
                Debug.WriteLine("Sync time: " + syncTime.ToString("O"));
                Debug.WriteLine("Now time: " + DateTime.UtcNow.ToString("O"));
                _captureSyncTimer.Elapsed += OnCaptureRequested;
                _captureSyncTimer.Interval = interval;
                _captureSyncTimer.Start();
            }
            catch (Exception e)
            {
                OnErrorOccurred(new ErrorEventArgs
                {
                    Exception = e,
                    Step = "Sync capture"
                });
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private async void ShowPairErrorOccurred(string step, string details)
        {
            await Device.InvokeOnMainThreadAsync(async () =>
            {
                await CurrentCoreMethods.DisplayAlert("Pair Error Occurred",
                    "An error occurred during " + step + ", exception: " + details, "OK");
            });
        }

        private async void ShowPairDisconnected()
        {
            await Device.InvokeOnMainThreadAsync(async () =>
            {
                await CurrentCoreMethods.DisplayAlert("Disconnected", "The connection to the paired device was lost. Please connect again.",
                    "OK");
            });
        }

        private async void ShowPairConnected()
        {
            await Device.InvokeOnMainThreadAsync(async () =>
            {
                await CurrentCoreMethods.DisplayAlert("Connected Pair Device", "Pair device connected successfully! This is the " + (_settings.IsPairedPrimary.HasValue && _settings.IsPairedPrimary.Value ? "primary" : "secondary") + " device.", "Yay");
            });
        }

        public async void SendLatestPreviewFrame(byte[] frame)
        {
            try
            {
                await _platformBluetooth.SendPreviewFrame(frame);
            }
            catch (Exception e)
            {
                OnErrorOccurred(new ErrorEventArgs
                {
                    Exception = e,
                    Step = "Send Preview Frame Outer"
                });
            }
        }

        public async void SendCapture(byte[] frame)
        {
            try
            {
                await _platformBluetooth.SendCaptue(frame);
            }
            catch (Exception e)
            {
                OnErrorOccurred(new ErrorEventArgs
                {
                    Exception = e,
                    Step = "Send Preview Frame Outer"
                });
            }
        }

        private async Task HandleBluetoothException(Exception e)
        {
            await Device.InvokeOnMainThreadAsync(async () =>
            {
                switch (e)
                {
                    case PermissionsException _:
                        await CurrentCoreMethods.DisplayAlert("Permissions Denied",
                            "The necessary permissions for pairing were not granted. Exception: " + e, "OK");
                        break;
                    case BluetoothNotSupportedException _:
                        await CurrentCoreMethods.DisplayAlert("Bluetooth Not Supported",
                            "Bluetooth is not supported on this device. Exception: " + e, "OK");
                        break;
                    case BluetoothFailedToSearchException _:
                        await CurrentCoreMethods.DisplayAlert("Failed to Search",
                            "The device failed to search for devices. Exception: " + e, "OK");
                        break;
                    case BluetoothNotTurnedOnException _:
                        await CurrentCoreMethods.DisplayAlert("Bluetooth Not On",
                            "The device failed to power on Bluetooth. Exception: " + e, "OK");
                        break;
                    default:
                        await CurrentCoreMethods.DisplayAlert("Error",
                            "An error occurred. Exception: " + e, "OK");
                        break;
                }
            });
        }

        private void AddDiscoveredDevice(object sender, PartnerDevice e)
        {
            if (DiscoveredDevices.All(d => d.Address != e.Address))
            {
                Device.BeginInvokeOnMainThread(() =>
                {
                    DiscoveredDevices.Add(e);
                });
            }
        }
    }

    public class ErrorEventArgs : EventArgs
    {
        public string Step { get; set; }
        public Exception Exception { get; set; }
    }

    public class PermissionsException : Exception {}
    public class BluetoothNotSupportedException : Exception {}
    public class BluetoothNotTurnedOnException : Exception {}
    public class BluetoothFailedToSearchException : Exception {}
}