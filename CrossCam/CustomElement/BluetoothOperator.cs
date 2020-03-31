using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Timers;
using CrossCam.Wrappers;
using FreshMvvm;
using Xamarin.Forms;

namespace CrossCam.CustomElement
{
    public sealed class BluetoothOperator : INotifyPropertyChanged
    {
        public bool IsConnected { get; set; }
        public bool IsPrimary => _platformBluetooth.IsPrimary;
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

        private const int TIMER_TOTAL_SAMPLES = 15;
        private int _timerSampleIndex;
        private readonly long[] T0_SAMPLES = new long[TIMER_TOTAL_SAMPLES];
        private readonly long[] T1T2_SAMPLES = new long[TIMER_TOTAL_SAMPLES];
        private readonly long[] T3_SAMPLES = new long[TIMER_TOTAL_SAMPLES];
        private bool _isCaptureRequested;

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
            var handler = Disconnected;
            handler?.Invoke(this, new EventArgs());
        }

        public event EventHandler Connected;
        private void OnConnected()
        {
            IsConnected = true;
            ShowPairConnected();
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

        public BluetoothOperator()
        {
            _platformBluetooth = DependencyService.Get<IPlatformBluetooth>();
            _platformBluetooth.DeviceDiscovered += PlatformBluetoothOnDeviceDiscovered;
            _platformBluetooth.Connected += PlatformBluetoothOnConnected;
            _platformBluetooth.Disconnected += PlatformBluetoothOnDisconnected;
            _platformBluetooth.PreviewFrameRequested += PlatformBluetoothOnPreviewFrameRequested;
            _platformBluetooth.PreviewFrameReceived += PlatformBluetoothOnPreviewFrameReceived;
            _platformBluetooth.ClockReadingReceived += PlatformBluetoothOnClockReadingReceived; 
            _platformBluetooth.SyncReceived += PlatformBluetoothOnSyncReceived;
            _platformBluetooth.CaptureReceived += PlatformBluetoothOnCaptureReceived;
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

        public async Task InitializeForPairingAndroid()
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

        public async Task InitializeForPairingiOSA()
        {
            _platformBluetooth.StartScanning();
        }

        public async Task InitializeForPairingiOSB()
        {
            await _platformBluetooth.BecomeDiscoverable();
        }

        public async Task InitializeForPairedConnection()
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

        public async void Connect(PartnerDevice device)
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

        public IEnumerable<PartnerDevice> GetPairedDevices()
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
            var offsetsAverage = 0L;
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

                offsetsAverage = (long)offsets.Average();
                tripsSafeAverage = (long)trips.Average();

                Debug.WriteLine("Average time offset: " + offsetsAverage / 10000d + " milliseconds");
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
            var partnerSyncMoment = targetSyncMoment.AddTicks(offsetsAverage);
            Debug.WriteLine("Partner sync: " + partnerSyncMoment.ToString("O"));
            SyncCapture(targetSyncMoment);
            await _platformBluetooth.SendSync(partnerSyncMoment);
            _isCaptureRequested = false;
        }

        private void SyncCapture(DateTime syncTime)
        {
            try
            {
                var interval = (syncTime.Ticks - DateTime.UtcNow.Ticks) / 10000d;
                Debug.WriteLine("Sync interval set: " + interval);
                Debug.WriteLine("Sync time: " + syncTime.ToString("O"));
                Debug.WriteLine("Now time: " + DateTime.UtcNow.ToString("O"));
                if (interval < 0)
                {
                    Debug.WriteLine("Sync aborted, interval < 0");
                    return;
                }

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
                await CurrentCoreMethods.DisplayAlert("Connected Pair Device", "Pair device connected successfully! This is the " + (_platformBluetooth.IsPrimary ? "primary" : "secondary") + " device.", "Yay");
            });
            if (!IsPrimary)
            {
                await _platformBluetooth.SayHello();
            }
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