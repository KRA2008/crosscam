using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
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
        public bool IsReadyForPreviewFrame { get; set; }

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

        public event EventHandler<byte[]> PreviewFrameAvailable;
        private void OnPreviewFrameAvailable(byte[] frame)
        {
            var handler = PreviewFrameAvailable;
            handler?.Invoke(this, frame);
        }

        public BluetoothOperator()
        {
            _platformBluetooth = DependencyService.Get<IPlatformBluetooth>();
            _platformBluetooth.DeviceDiscovered += PlatformBluetoothOnDeviceDiscovered;
            _platformBluetooth.Connected += PlatformBluetoothOnConnected;
            _platformBluetooth.Disconnected += PlatformBluetoothOnDisconnected;
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

        public async void RequestSyncForCaptureAndSync()
        {
            const int NUMBER_OF_RUNS = 10;
            const int BUFFER_TRIP_MULTIPLIER = 4;
            var offsets = new List<long>();
            var trips = new List<long>();
            var offsetsAverage = 0L;
            var tripsSafeAverage = 0L;
            var measureStart = DateTime.UtcNow.Ticks;
            try
            {
                for (var i = 0; i < NUMBER_OF_RUNS; i++)
                {
                    var t0 = DateTime.UtcNow.Ticks;
                    var readResult = new byte[0] { };//}await _device.ReadCharacteristic(_serviceGuid, _clockReadingGuid).ToTask();
                    var t3 = DateTime.UtcNow.Ticks;
                    var t1t2String = Encoding.UTF8.GetString(readResult, 0, readResult.Length);
                    if (long.TryParse(t1t2String, out var t1t2))
                    {
                        var timeOffsetRun = ((t1t2 - t0) + (t1t2 - t3)) / 2;
                        offsets.Add(timeOffsetRun);
                        var roundTripDelayRun = t3 - t0;
                        trips.Add(roundTripDelayRun);
                        Debug.WriteLine("Time offset: " + timeOffsetRun / 10000d + " milliseconds");
                        Debug.WriteLine("Round trip delay: " + roundTripDelayRun / 10000d + " milliseconds");
                    }
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

            var measureEnd = DateTime.UtcNow.Ticks;
            var measureDuration = measureEnd - measureStart;
            Debug.WriteLine("Measure duration: " + measureDuration / 10000d + " milliseconds");
            var targetSyncMoment = DateTime.UtcNow.AddTicks(tripsSafeAverage * BUFFER_TRIP_MULTIPLIER);
            Debug.WriteLine("Target sync: " + targetSyncMoment.ToString("O"));
            var partnerSyncMoment = targetSyncMoment.AddTicks(offsetsAverage);
            Debug.WriteLine("Partner sync: " + partnerSyncMoment.ToString("O"));
            //_device.WriteCharacteristic(_serviceGuid, _initiateCaptureGuid, Encoding.UTF8.GetBytes(partnerSyncMoment.Ticks.ToString())).Subscribe(what =>
            //{
            //    SyncCapture(targetSyncMoment);
            //}, exception =>
            //{
            //    OnErrorOccurred(new ErrorEventArgs
            //    {
            //        Exception = exception,
            //        Step = "Sending Trigger"
            //    });
            //});
        }

        private void HandleIncomingSyncRequest(byte[] masterTicksByteArray)
        {
            try
            {
                var targetTimeString = Encoding.UTF8.GetString(masterTicksByteArray, 0, masterTicksByteArray.Length);
                if (long.TryParse(targetTimeString, out var targetTimeTicks))
                {
                    var targetTime = new DateTime(targetTimeTicks);
                    SyncCapture(targetTime);
                    Debug.WriteLine("Target time: " + targetTime.ToString("O"));
                }
            }
            catch (Exception e)
            {
                OnErrorOccurred(new ErrorEventArgs
                {
                    Exception = e,
                    Step = "Receive Sync for Capture"
                });
            }
        }

        private void SyncCapture(DateTime syncTime)
        {
            var interval = (syncTime.Ticks - DateTime.UtcNow.Ticks) / 10000d;
            Debug.WriteLine("Sync interval set: " + interval);
            Debug.WriteLine("Sync time: " + syncTime.ToString("O"));
            if (interval < 0)
            {
                Debug.WriteLine("Sync aborted, interval < 0");
                return;
            }
            _captureSyncTimer.Elapsed += OnCaptureRequested;
            _captureSyncTimer.Interval = interval;
            _captureSyncTimer.Start();
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
            if (IsPrimary)
            {
                await _platformBluetooth.SayHello();
            }
        }

        public void SendLatestPreviewFrame(byte[] frame)
        {
            IsReadyForPreviewFrame = false;
            try
            {
                if (_device != null)
                {
                    if (frame != null /*&& _previewFrameCharacteristic != null*/)
                    {
                        const double INCREMENT = 0.01;
                        var threshold = INCREMENT;
                        double position;
                        double length;
                        double proportion;
                        //_previewFrameCharacteristic.BlobWrite(frame, false).Subscribe(resp =>
                        //{
                        //    position = resp.Position;
                        //    length = resp.TotalLength;
                        //    proportion = position / length;
                        //    if(proportion > threshold)
                        //    {
                        //        Debug.WriteLine("Complete: " + proportion);
                        //        threshold += INCREMENT;
                        //    }
                        //}, exception =>
                        //{
                        //    OnErrorOccurred(new ErrorEventArgs
                        //    {
                        //        Exception = exception,
                        //        Step = "Send Preview Frame"
                        //    });
                        //});
                    }
                }
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