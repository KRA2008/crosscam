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
using CrossCam.ViewModel;
using CrossCam.Wrappers;
using FreshMvvm;
using Xamarin.Forms;
using Timer = System.Timers.Timer;

namespace CrossCam.CustomElement
{
    public sealed class BluetoothOperator : INotifyPropertyChanged
    {
        private readonly Settings _settings;
        public bool IsPrimary => _settings.IsPairedPrimary.HasValue && _settings.IsPairedPrimary.Value;

        public double Fov //TODO: make field of view correction a value configured by user for a pair of phones
        {
            get => 90;
            set {}
        } 
        public double PartnerFov //TODO: make field of view correction a value configured by user for a pair of phones
        {
            get => 90;
            set {}
        }

        public IPageModelCoreMethods CurrentCoreMethods { get; set; }

        private readonly IPlatformBluetooth _platformBluetooth;
        public const string CROSSCAM_SERVICE = "CrossCam";
        private const int SECONDS_COUNTDOWN = 3;
        public static readonly Guid ServiceGuid = Guid.Parse("492a8e3d-2589-40b1-b9c2-419a7ce80f3c");
        private PartnerDevice _device;
        private readonly Timer _captureSyncTimer = new Timer{AutoReset = false};
        private readonly Timer _countdownTimer = new Timer{AutoReset = false};

        public decimal InitialSyncProgress { get; set; }
        private int TimerTotalSamples => _settings.PairSyncSampleCount;
        private int _timerSampleIndex;
        private int _timerSampleInitialIndex;
        private long[] _t0Samples;
        private long[] _t1t2Samples;
        private long[] _t3Samples;
        private bool _isCaptureRequested;

        public PairStatus PairStatus { get; set; }
        public int CountdownTimeRemaining { get; set; }

        private bool _primaryIsRequestingDisconnect;
        private int _initializeThreadLocker;
        private int _pairInitializeThreadLocker;
        private int _connectThreadLocker;

        public const int HEADER_LENGTH = 6;
        private const byte SYNC_MASK = 170; // 0xAA (do it twice)
        public enum CrossCommand
        {
            Fov = 1,
            RequestPreviewFrame,
            PreviewFrame,
            RequestClockReading,
            ClockReading,
            Sync,
            CapturedImage,
            Error
        }

        public event EventHandler Disconnected;
        private void OnDisconnected()
        {
            PairStatus = PairStatus.Disconnected;
            var handler = Disconnected;
            handler?.Invoke(this, new EventArgs());
        }

        public event EventHandler Connected;
        private void OnConnected()
        {
            PairStatus = PairStatus.Connected;
            if (!IsPrimary)
            {
                SendFov(Fov);
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

        public event EventHandler InitialSyncStarted;
        private void OnInitialSyncStarted()
        {
            var handler = InitialSyncStarted;
            handler?.Invoke(this, new EventArgs());
        }

        public event EventHandler InitialSyncCompleted;
        private void OnInitialSyncCompleted()
        {
            var handler = InitialSyncCompleted;
            handler?.Invoke(this, new EventArgs());
        }

        public event EventHandler PreviewFrameRequestReceived;
        private void OnPreviewFrameRequested()
        {
            var handler = PreviewFrameRequestReceived;
            handler?.Invoke(this, null);
        }

        public event EventHandler<byte[]> PreviewFrameReceived;
        private void OnPreviewFrameReceived(byte[] frame)
        {
            var handler = PreviewFrameReceived;
            handler?.Invoke(this, frame);
        }

        public event ElapsedEventHandler CaptureSyncTimeElapsed;
        private void OnCaptureSyncTimeElapsed(object sender, ElapsedEventArgs e)
        {
            Debug.WriteLine("CAPTURE NOW!!!!");
            _captureSyncTimer.Elapsed -= OnCaptureSyncTimeElapsed;
            var handler = CaptureSyncTimeElapsed;
            handler?.Invoke(this, e);
        }

        private void OnCountdownTimerSecondElapsed(object sender, ElapsedEventArgs e)
        {
            CountdownTimeRemaining--;
            _countdownTimer.Elapsed -= OnCountdownTimerSecondElapsed;
            if (CountdownTimeRemaining > 0)
            {
                _countdownTimer.Interval = 1000;
                _countdownTimer.Elapsed += OnCountdownTimerSecondElapsed;
                _countdownTimer.Start();
            }
        }

        private void OnCountdownTimerSyncComplete(object sender, ElapsedEventArgs e)
        {
            CountdownTimeRemaining = SECONDS_COUNTDOWN;
            _countdownTimer.Elapsed -= OnCountdownTimerSyncComplete;
            _countdownTimer.Interval = 1000;
            _countdownTimer.Elapsed += OnCountdownTimerSecondElapsed;
            _countdownTimer.Start();
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
            _platformBluetooth.Connected += PlatformBluetoothOnConnected;
            _platformBluetooth.Disconnected += PlatformBluetoothOnDisconnected;
            _platformBluetooth.PayloadReceived += PlatformBluetoothOnPayloadReceived;
        }

        private void PlatformBluetoothOnPayloadReceived(object sender, byte[] bytes)
        {
            if (bytes.Length >= HEADER_LENGTH)
            {
                if (bytes[0] == SYNC_MASK &&
                    bytes[1] == SYNC_MASK)
                {
                    var payloadLength = (bytes[3] << 16) | (bytes[4] << 8) | bytes[5];
                    var payload = bytes.Skip(HEADER_LENGTH).ToArray();
                    if (payload.Length != payloadLength)
                    {
                        Debug.WriteLine("### payload stated length (" + payload.Length + ") was not equal to payload observed length (" + payloadLength + ")");
                    }

                    //if ((CrossCommand) bytes[2] != CrossCommand.PreviewFrame &&
                    //    (CrossCommand) bytes[2] != CrossCommand.CapturedImage)
                    //{
                    //    Debug.WriteLine("### PAYLOAD RECEIVED: " + string.Join(",",bytes));
                    //}

                    //Debug.WriteLine("### Command received: " + (CrossCommand)bytes[2]);
                    switch (bytes[2])
                    {
                        case (byte)CrossCommand.Fov:
                            HandleFovReceived(payload);
                            break;
                        case (byte)CrossCommand.RequestPreviewFrame:
                            OnPreviewFrameRequested();
                            break;
                        case (byte)CrossCommand.PreviewFrame:
                            OnPreviewFrameReceived(payload);
                            break;
                        case (byte)CrossCommand.RequestClockReading:
                            SendClockReading();
                            break;
                        case (byte)CrossCommand.ClockReading:
                            ProcessClockReading(payload);
                            break;
                        case (byte)CrossCommand.Sync:
                            ProcessSync(payload);
                            break;
                        case (byte)CrossCommand.CapturedImage:
                            OnCapturedImageReceived(payload);
                            break;
                        case (byte)CrossCommand.Error:
                            SecondaryErrorReceived();
                            //_platformBluetooth.OnSecondaryErrorReceived();
                            // TODO: handle
                            break;
                    }
                }
            }
            else
            {
                Debug.WriteLine("### payload received with header too short???");
            }
        }

        private void SendReadyForPreviewFrame()
        {
            var fullMessage = AddPayloadHeader(CrossCommand.RequestPreviewFrame, Enumerable.Empty<byte>().ToArray());
            _platformBluetooth.SendPayload(fullMessage);
        }

        private void SendPreviewFrame(byte[] frame, byte? rotationNeeded = null)
        {
            var frameMessage = AddPayloadHeader(CrossCommand.PreviewFrame, rotationNeeded.HasValue ? frame.Concat(new []{rotationNeeded.Value}).ToArray() : frame);
            _platformBluetooth.SendPayload(frameMessage);
        }

        private void SendReadyForClockReading()
        {
            var message = AddPayloadHeader(CrossCommand.RequestClockReading, Enumerable.Empty<byte>().ToArray());
            _platformBluetooth.SendPayload(message);
        }

        private void SendSecondaryErrorOccurred()
        {
            _platformBluetooth.SendPayload(AddPayloadHeader(CrossCommand.Error, Enumerable.Empty<byte>().ToArray()));
        }

        private void SendFov(double fov)
        {
            var payloadBytes = BitConverter.GetBytes(fov);
            var fullMessage = AddPayloadHeader(CrossCommand.Fov, payloadBytes);

            _platformBluetooth.SendPayload(fullMessage);
        }

        private void SendSync(DateTime syncMoment)
        {
            var syncMessage = AddPayloadHeader(CrossCommand.Sync, BitConverter.GetBytes(syncMoment.Ticks));
            _platformBluetooth.SendPayload(syncMessage);
        }

        private void SendClockReading()
        {
            var ticks = DateTime.UtcNow.Ticks;
            var message = AddPayloadHeader(CrossCommand.ClockReading, BitConverter.GetBytes(ticks));
            _platformBluetooth.SendPayload(message);
        }

        private static byte[] AddPayloadHeader(CrossCommand crossCommand, byte[] payload)
        {
            //Debug.WriteLine("### Command sending: " + crossCommand);
            var payloadLength = payload.Length;
            var header = new List<byte>
            {
                SYNC_MASK,
                SYNC_MASK,
                (byte) crossCommand,
                (byte) (payloadLength >> 16),
                (byte) ((payloadLength >> 8) & 0x00ff),
                (byte) (payloadLength & 0x0000ff)
            };
            var fullPayload = header.Concat(payload).ToArray();
            //if (crossCommand != CrossCommand.CapturedImage &&
            //    crossCommand != CrossCommand.PreviewFrame)
            //{
            //    Debug.WriteLine("### PAYLOAD SENT: " + string.Join(",", fullPayload));
            //}
            return fullPayload;
        }

        private void HandleFovReceived(byte[] bytes)
        {
            //Debug.WriteLine("Received fov");
            var fov = BitConverter.ToDouble(bytes, 0);
            OnConnected();
            PartnerFov = fov;
            RequestClockReading();
        }

        private void SecondaryErrorReceived()
        {
            OnErrorOccurred(new ErrorEventArgs
            {
                Exception = new Exception(),
                Step = "secondary device operation"
            });
        }

        public async Task SetUpPrimaryForPairing()
        {
            if (Interlocked.CompareExchange(ref _initializeThreadLocker, 1, 0) == 0)
            {
                try
                {
                    PairStatus = PairStatus.Connecting;
                    _primaryIsRequestingDisconnect = false;
                   var resultMessage = await _platformBluetooth.StartScanning();
                   if (resultMessage != null)
                   {
                       await HandleBluetoothException(new Exception(resultMessage));
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
                    PairStatus = PairStatus.Connecting;
                    await _platformBluetooth.BecomeDiscoverable();
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

        private void ProcessSync(byte[] syncBytes)
        {
            SyncCapture(new DateTime(BitConverter.ToInt64(syncBytes, 0)));
        }

        private async void ProcessClockReading(byte[] readingBytes)
        {
            var reading = BitConverter.ToInt64(readingBytes, 0);
            _t1t2Samples[_timerSampleIndex] = reading;
            _t3Samples[_timerSampleIndex] = DateTime.UtcNow.Ticks;
            if (_timerSampleIndex < TimerTotalSamples - 1)
            {
                _timerSampleIndex++;
            }
            else
            {
                Debug.WriteLine("### TIMER SAMPLES WRAPPED");
                _timerSampleIndex = 0;
            }

            if (_primaryIsRequestingDisconnect)
            {
                _platformBluetooth.Disconnect();
                _primaryIsRequestingDisconnect = false;
            }
            else
            {
                if (!_isCaptureRequested)
                {
                    await Task.Delay(_settings.PairedPreviewFrameDelayMs);
                    if (_timerSampleInitialIndex < TimerTotalSamples)
                    {
                        _timerSampleInitialIndex++;
                        InitialSyncProgress = decimal.Round(100m * _timerSampleInitialIndex / (1m * TimerTotalSamples));
                        RequestClockReading();
                        OnInitialSyncStarted();
                    }
                    else
                    {
                        OnInitialSyncCompleted();
                        SendReadyForPreviewFrame();
                    }
                }
                else
                {
                    CalculateAndApplySyncMoment();
                }
            }
        }

        private void PlatformBluetoothOnDisconnected(object sender, EventArgs e)
        {
            OnDisconnected();
        }

        private void PlatformBluetoothOnConnected(object sender, EventArgs e)
        {
            _timerSampleInitialIndex = 0;
            InitialSyncProgress = 0;
            _t0Samples = new long[TimerTotalSamples];
            _t1t2Samples = new long[TimerTotalSamples];
            _t3Samples = new long[TimerTotalSamples];
            OnConnected();
        }

        public void Disconnect()
        {
            if (IsPrimary && PairStatus == PairStatus.Connected)
            {
                _primaryIsRequestingDisconnect = true;
            }
            else
            {
                _platformBluetooth.Disconnect();
            }
            OnDisconnected();
        }

        public void BeginSyncedCapture()
        {
            _isCaptureRequested = true;
        }

        public void RequestClockReading()
        {
            _t0Samples[_timerSampleIndex] = DateTime.UtcNow.Ticks;
            SendReadyForClockReading();
        }

        private void CalculateAndApplySyncMoment()
        {
            try
            {
                var offsets = new List<long>();
                var trips = new List<long>();
                var offset = 0L;
                var trip = 0L;
                try
                {
                    for (var i = 0; i < TimerTotalSamples; i++)
                    {
                        var t0 = _t0Samples[i];
                        var t3 = _t3Samples[i];
                        var t1t2 = _t1t2Samples[i];

                        var timeOffsetRun = ((t1t2 - t0) + (t1t2 - t3)) / 2;
                        offsets.Add(timeOffsetRun);
                        var roundTripDelayRun = t3 - t0;
                        trips.Add(roundTripDelayRun);
                    }

                    offset = GetCleanAverage(offsets);
                    trip = trips.Max();
                }
                catch (Exception e)
                {
                    OnErrorOccurred(new ErrorEventArgs
                    {
                        Exception = e,
                        Step = "send read time request"
                    });
                }

                Debug.WriteLine("OFFSETS: " + string.Join(",", offsets));
                Debug.WriteLine("CLEANED OFFSET: " + offset);
                Debug.WriteLine("TRIPS: " + string.Join(",", trips));
                Debug.WriteLine("MAX TRIP: " + trip);

                //var targetSyncMoment = DateTime.UtcNow.AddTicks(trip * BUFFER_TRIP_MULTIPLIER);
                var targetSyncMoment = DateTime.UtcNow.AddSeconds(SECONDS_COUNTDOWN);
                SyncCapture(targetSyncMoment);
                var partnerSyncMoment = targetSyncMoment.AddTicks(offset);
                Debug.WriteLine("Target sync: " + targetSyncMoment.ToString("O"));
                Debug.WriteLine("Partner sync: " + partnerSyncMoment.ToString("O"));
                SendSync(partnerSyncMoment);
                _isCaptureRequested = false;
            }
            catch (Exception e)
            {
                OnErrorOccurred(new ErrorEventArgs
                {
                    Exception = e,
                    Step = "calc sync"
                });
            }
        }

        private static long GetCleanAverage(IEnumerable<long> data)
        {
            var nonZero = data.Where(d => d != 0).ToList();

            var avg = nonZero.Average();
            var sum = nonZero.Sum(d => Math.Pow(d - avg, 2));
            var stdDev = Math.Sqrt(sum / nonZero.Count);

            return (long)nonZero.Where(datum => Math.Abs(datum - avg) < stdDev).Average();
        }

        private void SyncCapture(DateTime syncTime)
        {
            try
            {
                var interval = (syncTime.Ticks - DateTime.UtcNow.Ticks) / 10000d;
                _captureSyncTimer.Elapsed += OnCaptureSyncTimeElapsed;
                _captureSyncTimer.Interval = interval;
                _captureSyncTimer.Start();
                _countdownTimer.Elapsed += OnCountdownTimerSyncComplete;
                _countdownTimer.Interval = SECONDS_COUNTDOWN * 1000 - interval;
                _countdownTimer.Start();
                Debug.WriteLine("Sync interval set: " + interval);
                Debug.WriteLine("Sync time: " + syncTime.ToString("O"));
            }
            catch (Exception e)
            {
                if (!IsPrimary)
                {
                    SendSecondaryErrorOccurred();
                }
                OnErrorOccurred(new ErrorEventArgs
                {
                    Exception = e,
                    Step = "sync capture"
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

        public void SendLatestPreviewFrame(byte[] frame, byte? rotationNeeded = null)
        {
            try
            {
                SendPreviewFrame(frame, rotationNeeded);
            }
            catch (Exception e)
            {
                OnErrorOccurred(new ErrorEventArgs
                {
                    Exception = e,
                    Step = "send preview frame"
                });
            }
        }

        public void SendCapture(byte[] frame)
        {
            try
            {
                var message = AddPayloadHeader(CrossCommand.CapturedImage, frame);
                _platformBluetooth.SendPayload(message);
            }
            catch (Exception e)
            {
                OnErrorOccurred(new ErrorEventArgs
                {
                    Exception = e,
                    Step = "send captured frame"
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