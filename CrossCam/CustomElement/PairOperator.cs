﻿using System;
using System.Collections.Generic;
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
    public sealed class PairOperator : INotifyPropertyChanged
    {
        private readonly Settings _settings;
        public bool IsPrimary => _settings.IsPairedPrimary.HasValue && _settings.IsPairedPrimary.Value;

        public IPageModelCoreMethods CurrentCoreMethods { get; set; }

        private readonly IPlatformPair _platformPair;
        public const string CROSSCAM_SERVICE = "CrossCam";
        private readonly Timer _captureSyncTimer = new Timer{AutoReset = false};
        private readonly Timer _countdownTimer = new Timer{AutoReset = false};

        public decimal InitialSyncProgress { get; set; }
        private int TimerTotalSamples => _settings.PairSyncSampleCount;
        private int _timerSampleIndex;
        private long[] _t0Samples;
        private long[] _t1t2Samples;
        private long[] _t3Samples;
        private bool _isCaptureRequested;
        private DateTime? _captureMomentUtc;
        private DateTime _lastPreviewFrameUtc;
        private int _requestingPreviewFrameInterlocked;

        public PairStatus PairStatus { get; set; }
        public int CountdownTimeRemainingSec { get; set; }
        
        private int _initializeThreadLocker;
        private bool _initialSyncComplete;

        private long _countdownTimeTicks;

        public const int HEADER_LENGTH = 6;
        private const byte SYNC_MASK = 170; // 0xAA (do it twice)
        public enum CrossCommand
        {
            Hello = 1,
            RequestPreviewFrame,
            PreviewFrame,
            RequestClockReading,
            ClockReading,
            Sync,
            CapturedImage,
            TransmissionComplete,
            Error
        }

        public event EventHandler Disconnected;
        private void OnDisconnected()
        {
            PairStatus = PairStatus.Disconnected;
            OnPropertyChanged(nameof(IsPrimary));
            OnPropertyChanged(nameof(PairStatus));
            OnPropertyChanged(nameof(CountdownTimeRemainingSec));
            _settings.RaisePropertyChanged(nameof(Settings.IsPairedPrimary));
            Disconnected?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler Connected;
        private void OnConnected()
        {
            PairStatus = PairStatus.Connected;
            if (!IsPrimary)
            {
                SendHello();
            }
            OnPropertyChanged(nameof(IsPrimary));
            OnPropertyChanged(nameof(PairStatus));
            OnPropertyChanged(nameof(CountdownTimeRemainingSec));
            _settings.RaisePropertyChanged(nameof(Settings.IsPairedPrimary));
            Connected?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler<ErrorEventArgs> ErrorOccurred;
        private void OnErrorOccurred(ErrorEventArgs e)
        {
            ShowPairErrorOccurred(e.Step, e.Exception.ToString());
            ErrorOccurred?.Invoke(this, e);
        }

        public event EventHandler InitialSyncStarted;
        private void OnInitialSyncStarted()
        {
            InitialSyncStarted?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler InitialSyncCompleted;
        private void OnInitialSyncCompleted()
        {
            _initialSyncComplete = true;
            _requestingPreviewFrameInterlocked = 0;
            InitialSyncCompleted?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler PreviewFrameRequestReceived;
        private void OnPreviewFrameRequested()
        {
            PreviewFrameRequestReceived?.Invoke(this, null);
        }

        public event EventHandler<byte[]> PreviewFrameReceived;
        private void OnPreviewFrameReceived(byte[] frame)
        {
            _requestingPreviewFrameInterlocked = 0;
            PreviewFrameReceived?.Invoke(this, frame);
        }

        public event EventHandler TransmissionStarted;
        private void OnTransmittingCaptureStarted()
        {
            TransmissionStarted?.Invoke(this, null);
        }

        public event EventHandler TransmissionComplete;
        private void OnTransmissionComplete()
        {
            TransmissionComplete?.Invoke(this, null);
        }

        public event ElapsedEventHandler CaptureSyncTimeElapsed;
        private void OnCaptureSyncTimeElapsed(object sender, ElapsedEventArgs e)
        {
            Debug.WriteLine("CAPTURE NOW!!!!");
            _captureSyncTimer.Elapsed -= OnCaptureSyncTimeElapsed;
            CaptureSyncTimeElapsed?.Invoke(this, e);
        }

        private void OnCountdownTimerSecondElapsed(object sender, ElapsedEventArgs e)
        {
            CountdownTimeRemainingSec--;
            _countdownTimer.Elapsed -= OnCountdownTimerSecondElapsed;
            if (CountdownTimeRemainingSec > 0)
            {
                _countdownTimer.Interval = 1000;
                _countdownTimer.Elapsed += OnCountdownTimerSecondElapsed;
                _countdownTimer.Start();
            }
        }

        public event EventHandler CountdownTimerSyncCompletePrimary;
        private void OnCountdownTimerSyncCompletePrimary(object sender, ElapsedEventArgs e)
        {
            CountdownTimeRemainingSec = _settings.PairedCaptureCountdown;
            _countdownTimer.Elapsed -= OnCountdownTimerSyncCompletePrimary;
            _countdownTimer.Interval = 1000;
            _countdownTimer.Elapsed += OnCountdownTimerSecondElapsed;
            _countdownTimer.Start();
            CountdownTimerSyncCompletePrimary?.Invoke(this, e);
        }

        public event EventHandler CountdownTimerSyncCompleteSecondary;
        private void OnCountdownTimerSyncCompleteSecondary(object sender, ElapsedEventArgs e)
        {
            CountdownTimerSyncCompleteSecondary?.Invoke(this, e);
        }

        public event EventHandler<byte[]> CapturedImageReceived;
        private void OnCapturedImageReceived(byte[] frame)
        {
            _captureMomentUtc = null;
            CapturedImageReceived?.Invoke(this, frame);
        }

        public PairOperator(Settings settings)
        {
            _settings = settings;
            _platformPair = DependencyService.Get<IPlatformPair>();
            _platformPair.Connected += PlatformPairOnConnected;
            _platformPair.Disconnected += PlatformPairOnDisconnected;
            _platformPair.PayloadReceived += PlatformPairOnPayloadReceived;
            _platformPair.ErrorOccurred += PlatformPairOnErrorOccurred;
        }

        private void PlatformPairOnErrorOccurred(object sender, ErrorEventArgs args)
        {
            OnErrorOccurred(args);
        }

        private void PlatformPairOnPayloadReceived(object sender, byte[] bytes)
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

                    //if ((CrossCommand) bytes[2] != CrossCommand.RemotePreviewFrame &&
                    //    (CrossCommand) bytes[2] != CrossCommand.CapturedImage)
                    //{
                    //    Debug.WriteLine("### PAYLOAD RECEIVED: " + string.Join(",",bytes));
                    //}

                    //Debug.WriteLine("### Command received: " + (CrossCommand)bytes[2]);
                    switch (bytes[2])
                    {
                        case (byte)CrossCommand.Hello:
                            HandleHelloReceived();
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
                        case (byte)CrossCommand.TransmissionComplete:
                            TransmissionCompleted();
                            break;
                    }
                }
            }
            else
            {
                Debug.WriteLine("### payload received with header too short???");
            }
        }

        private void TransmissionCompleted()
        {
            OnTransmissionComplete();
        }

        private void SendReadyForPreviewFrame()
        {
            var fullMessage = AddPayloadHeader(CrossCommand.RequestPreviewFrame, Enumerable.Empty<byte>().ToArray());
            _platformPair.SendPayload(fullMessage);
        }

        private void SendPreviewFrame(byte[] frame, byte? rotationNeeded = null)
        {
            var frameMessage = AddPayloadHeader(CrossCommand.PreviewFrame, rotationNeeded.HasValue ? frame.Concat(new []{rotationNeeded.Value}).ToArray() : frame);
            _platformPair.SendPayload(frameMessage);
        }

        private void SendReadyForClockReading()
        {
            var message = AddPayloadHeader(CrossCommand.RequestClockReading, Enumerable.Empty<byte>().ToArray());
            _platformPair.SendPayload(message);
        }

        private void SendSecondaryErrorOccurred()
        {
            _platformPair.SendPayload(AddPayloadHeader(CrossCommand.Error, Enumerable.Empty<byte>().ToArray()));
        }

        private void SendHello()
        {
            var fullMessage = AddPayloadHeader(CrossCommand.Hello, Enumerable.Empty<byte>().ToArray());
            _platformPair.SendPayload(fullMessage);
        }

        private void SendSync(DateTime syncMoment)
        {
            var syncMessage = AddPayloadHeader(CrossCommand.Sync, BitConverter.GetBytes(syncMoment.Ticks));
            _platformPair.SendPayload(syncMessage);
        }

        private void SendClockReading()
        {
            var ticks = DateTime.UtcNow.Ticks;
            var message = AddPayloadHeader(CrossCommand.ClockReading, BitConverter.GetBytes(ticks));
            _platformPair.SendPayload(message);
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
            //    crossCommand != CrossCommand.RemotePreviewFrame)
            //{
            //    Debug.WriteLine("### PAYLOAD SENT: " + string.Join(",", fullPayload));
            //}
            return fullPayload;
        }

        private void HandleHelloReceived()
        {
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
                   await _platformPair.StartScanning();
                }
                catch (Exception e)
                {
                    _platformPair.Disconnect();
                    await HandlePairException(e);
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
                    await _platformPair.BecomeDiscoverable();
                }
                catch (Exception e)
                {
                    _platformPair.Disconnect();
                    await HandlePairException(e);
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

        private void ProcessClockReading(byte[] readingBytes)
        {
            if (_timerSampleIndex < TimerTotalSamples)
            {
                _t3Samples[_timerSampleIndex] = DateTime.UtcNow.Ticks;
                if (_timerSampleIndex == 0)
                {
                    OnInitialSyncStarted();
                }
                var reading = BitConverter.ToInt64(readingBytes, 0);
                _t1t2Samples[_timerSampleIndex] = reading;
                _timerSampleIndex++;
                InitialSyncProgress = decimal.Round(100m * _timerSampleIndex / (1m * TimerTotalSamples));
                RequestClockReading();
            }
            else
            {
                OnInitialSyncCompleted();
            }
        }

        private void PlatformPairOnDisconnected(object sender, EventArgs e)
        {
            OnDisconnected();
        }

        private void PlatformPairOnConnected(object sender, EventArgs e)
        {
            _timerSampleIndex = 0;
            _initialSyncComplete = false;
            InitialSyncProgress = 0;
            _t0Samples = new long[TimerTotalSamples];
            _t1t2Samples = new long[TimerTotalSamples];
            _t3Samples = new long[TimerTotalSamples];
            OnConnected();
        }

        public void Disconnect()
        {
            _platformPair.Disconnect();
        }

        public void BeginSyncedCapture()
        {
            _isCaptureRequested = true;
        }

        private void RequestClockReading()
        {
            if (_timerSampleIndex < TimerTotalSamples)
            {
                _t0Samples[_timerSampleIndex] = DateTime.UtcNow.Ticks;
            }
            SendReadyForClockReading();
        }

        private void CalculateAndApplySyncMoment()
        {
            try
            {
                _isCaptureRequested = false;
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

                //Debug.WriteLine("OFFSETS: " + string.Join(",", offsets));
                //Debug.WriteLine("CLEANED OFFSET: " + offset);
                //Debug.WriteLine("TRIPS: " + string.Join(",", trips));
                //Debug.WriteLine("MAX TRIP: " + trip);

                if (_settings.PairedCaptureCountdown > 0)
                {
                    _countdownTimeTicks = _settings.PairedCaptureCountdown * 1000 * 10000;
                }
                else
                {
                    _countdownTimeTicks = trip;
                }
                var targetSyncMoment = DateTime.UtcNow.AddTicks(_countdownTimeTicks);
                var partnerSyncMoment = targetSyncMoment.AddTicks(offset);
                SyncCapture(targetSyncMoment);
                //Debug.WriteLine("Target sync: " + targetSyncMoment.ToString("O"));
                //Debug.WriteLine("Partner sync: " + partnerSyncMoment.ToString("O"));
                SendSync(partnerSyncMoment);
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
                if (_settings.IsPairedPrimary.HasValue &&
                    _settings.IsPairedPrimary.Value &&
                    _settings.PairedCaptureCountdown > 0)
                {
                    _countdownTimer.Elapsed += OnCountdownTimerSyncCompletePrimary;
                    _countdownTimer.Interval = _settings.PairedCaptureCountdown * 1000 - interval;
                    _countdownTimer.Start();
                } 
                else if (_settings.IsPairedPrimary.HasValue &&
                         !_settings.IsPairedPrimary.Value)
                {
                    OnCountdownTimerSyncCompleteSecondary(null, null);
                }
                //Debug.WriteLine("Sync interval set: " + interval);
                //Debug.WriteLine("Sync time: " + syncTime.ToString("O"));
                _captureMomentUtc = syncTime;
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

        public void SendLatestPreviewFrame(byte[] frame, byte? rotationNeeded = null) //TODO: the optional parameter is never used?
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
                OnTransmittingCaptureStarted();
                _platformPair.SendPayload(message);
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

        public void SendTransmissionComplete()
        {
            try
            {
                var message = AddPayloadHeader(CrossCommand.TransmissionComplete, Enumerable.Empty<byte>().ToArray());
                _platformPair.SendPayload(message);
            }
            catch (Exception e)
            {
                OnErrorOccurred(new ErrorEventArgs
                {
                    Exception = e,
                    Step = "send transmission complete"
                });
            }
        }

        private async Task HandlePairException(Exception e)
        {
            await Device.InvokeOnMainThreadAsync(async () =>
            {
                switch (e)
                {
                    case PermissionsException _:
                        await CurrentCoreMethods.DisplayAlert("Permissions Denied",
                            "The necessary permissions for pairing were not granted. Exception: " + e, "OK");
                        break;
                    case PairingNotSupportedException _:
                        await CurrentCoreMethods.DisplayAlert("Pairing Not Supported",
                            "Pairing is not supported on this device. Exception: " + e, "OK");
                        break;
                    case PairingFailedToSearchException _:
                        await CurrentCoreMethods.DisplayAlert("Failed to Search",
                            "The device failed to search for devices. Exception: " + e, "OK");
                        break;
                    case BluetoothNotTurnedOnException _:
                        await CurrentCoreMethods.DisplayAlert("Bluetooth Not On",
                            "The device failed to power on Bluetooth. Exception: " + e, "OK");
                        break;
                    case WiFiTurnedOffException _:
                        await CurrentCoreMethods.DisplayAlert("Wi-Fi Off", 
                            "Wi-Fi is turned off. Please turn Wi-Fi on.", "OK");
                        break;
                    case LocationServicesNotEnabledException _:
                        await CurrentCoreMethods.DisplayAlert("Location Services Needed",
                            "Location services not activated. Cannot scan for devices. See the pairing page for more details.", "OK");
                        break;
                    case LocationPermissionNotGrantedException _:
                        await CurrentCoreMethods.DisplayAlert("Location Permission Needed",
                            "Location permission not granted. Cannot scan for devices. See the pairing page for more details.", "OK");
                        break;
                    default:
                        throw e;
                }
            });
        }

        public void RequestPreviewFrame()
        {
            try
            {
                if (IsPrimary &&
                    PairStatus == PairStatus.Connected && 
                    _initialSyncComplete)
                {
                    if (!_isCaptureRequested)
                    {
                        if (_initialSyncComplete &&
                            (!_captureMomentUtc.HasValue ||
                            _captureMomentUtc.Value > DateTime.UtcNow.AddSeconds(1).AddMilliseconds(_settings.PairedPreviewFrameDelayMs)) &&
                            _lastPreviewFrameUtc < DateTime.UtcNow.AddMilliseconds(-_settings.PairedPreviewFrameDelayMs) &&
                            Interlocked.CompareExchange(ref _requestingPreviewFrameInterlocked, 1, 0) == 0)
                        {
                            _lastPreviewFrameUtc = DateTime.UtcNow;
                            SendReadyForPreviewFrame();
                        }
                    }
                    else
                    {
                        CalculateAndApplySyncMoment();
                    }
                }
            }
            catch (Exception e)
            {
                OnErrorOccurred(new ErrorEventArgs
                {
                    Exception = e,
                    Step = "request preview"
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
    public class PairingNotSupportedException : Exception {}
    public class BluetoothNotTurnedOnException : Exception {}
    public class PairingFailedToSearchException : Exception {}
    public class WiFiTurnedOffException : Exception {}
    public class LocationServicesNotEnabledException : Exception {}
    public class LocationPermissionNotGrantedException : Exception {}
}