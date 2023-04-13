﻿using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Android.Bluetooth;
using Android.Gms.Common;
using Android.Gms.Common.Apis;
using Android.Gms.Location;
using Android.Gms.Nearby;
using Android.Gms.Nearby.Connection;
using Android.OS;
using AndroidX.AppCompat.App;
using CrossCam.CustomElement;
using CrossCam.Droid.CustomRenderer;
using CrossCam.Wrappers;
using Xamarin.Forms;
using Xamarin.Essentials;
using Debug = System.Diagnostics.Debug;
using ErrorEventArgs = CrossCam.CustomElement.ErrorEventArgs;

[assembly: Dependency(typeof(PlatformPair))]
namespace CrossCam.Droid.CustomRenderer
{
    public sealed class PlatformPair : BluetoothGattCallback, IPlatformPair
    {
        public static TaskCompletionSource<bool> BluetoothPermissionsTask;
        public static TaskCompletionSource<bool> LocationPermissionsTask;
        public static TaskCompletionSource<bool> TurnOnLocationTask;

        private readonly IConnectionsClient _client;
        private string _connectedPartnerId;

        public PlatformPair()
        {
            _client = NearbyClass.GetConnectionsClient(MainActivity.Instance);
        }

        public void Disconnect()
        {
            Debug.WriteLine("### Disconnecting");
            if (!string.IsNullOrWhiteSpace(_connectedPartnerId))
            {
                _client.DisconnectFromEndpoint(_connectedPartnerId);
            }
            _client.StopDiscovery();
            _client.StopAdvertising();
            _client.StopAllEndpoints();
            OnDisconnected();
        }

        private static Task<bool> RequestBluetoothPermissions()
        {
            BluetoothPermissionsTask = new TaskCompletionSource<bool>();
            MainActivity.Instance.CheckForAndRequestBluetoothPermissions();
            return BluetoothPermissionsTask.Task;
        }

        private static Task<bool> RequestLocationPermissions()
        {
            LocationPermissionsTask = new TaskCompletionSource<bool>();
            MainActivity.Instance.CheckForAndRequestLocationPermissions();
            return LocationPermissionsTask.Task;
        }

        private static Task<bool> TurnOnLocationServices()
        {
            return CheckForAndTurnOnLocationServices();
        }

        public static async Task<bool> CheckForAndTurnOnLocationServices(bool checkOnly = false)
        {
            //Debug.WriteLine("### DoingLocationStuff");
            if (!checkOnly)
            {
                TurnOnLocationTask = new TaskCompletionSource<bool>();
            }

            try
            {
                var builder = new LocationSettingsRequest.Builder().AddLocationRequest(LocationRequest.Create())
                    .SetAlwaysShow(true);

                var response = await LocationServices.GetSettingsClient(MainActivity.Instance)
                    .CheckLocationSettingsAsync(builder.Build());
                TurnOnLocationTask.SetResult(response.LocationSettingsStates.IsLocationUsable);
            }
            catch (Exception e)
            {
                if (e is ResolvableApiException exc &&
                    !checkOnly)
                {
                    exc.StartResolutionForResult(MainActivity.Instance,(int)MainActivity.RequestCodes.TurnLocationServicesOnRequestCode);
                    return await TurnOnLocationTask.Task;
                }
                Debug.WriteLine("### Location error: " + e);
                TurnOnLocationTask.SetResult(false);
            }

            return await TurnOnLocationTask.Task;
        }

        public async void SendPayload(byte[] bytes)
        {
            try
            {
                if ((PairOperator.CrossCommand)bytes[2] == PairOperator.CrossCommand.CapturedImage)
                {
                    await Task.Delay(1000);
                }
                using var payload = Payload.FromStream(new MemoryStream(bytes));
                await _client.SendPayloadAsync(_connectedPartnerId, payload);
            }
            catch (Exception e)
            {
                OnErrorOccurred(new ErrorEventArgs
                {
                    Exception = e,
                    Step = "Send payload"
                });
            }
        }

        public event EventHandler<byte[]> PayloadReceived;
        private void ProcessReceivedPayload(byte[] payload)
        {
            PayloadReceived?.Invoke(this, payload);
        }

        public event EventHandler<ErrorEventArgs> ErrorOccurred;
        private void OnErrorOccurred(ErrorEventArgs error)
        {
            ErrorOccurred?.Invoke(this, error);
        }

        public event EventHandler Connected;
        private void OnConnected()
        {
            Connected?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler Disconnected;
        private void OnDisconnected()
        {
            Disconnected?.Invoke(this, EventArgs.Empty);
        }

        public async Task BecomeDiscoverable()
        {
            if (!await RequestBluetoothPermissions()) throw new PermissionsException();

            await _client.StartAdvertisingAsync(DeviceInfo.Name, PairOperator.CROSSCAM_SERVICE,
                new MyConnectionLifecycleCallback(this), new AdvertisingOptions.Builder().SetStrategy(Strategy.P2pPointToPoint).Build());
        }

        public async Task StartScanning()
        {
            //Debug.WriteLine("### StartingScanning");
            if (!await RequestBluetoothPermissions()) throw new PermissionsException();
            if (!await RequestLocationPermissions()) throw new LocationPermissionNotGrantedException();
            if (!await TurnOnLocationServices()) throw new LocationServicesNotEnabledException();
            
            await _client.StartDiscoveryAsync(PairOperator.CROSSCAM_SERVICE, new MyEndpointDiscoveryCallback(this),
                new DiscoveryOptions.Builder().SetStrategy(Strategy.P2pPointToPoint).Build());
        }

        private class MyConnectionLifecycleCallback : ConnectionLifecycleCallback
        {
            private readonly PlatformPair _platformPair;

            public MyConnectionLifecycleCallback(PlatformPair platformPair)
            {
                _platformPair = platformPair;
            }

            public override void OnConnectionInitiated(string p0, ConnectionInfo p1)
            {
                Debug.WriteLine("### OnConnectionInitiated: " + p0 + ", " + p1.EndpointName);
                new AlertDialog.Builder(MainActivity.Instance).SetTitle("Accept connection to " + p1.EndpointName + "?")
                    .SetMessage("Confirm the code matches on both devices: " + p1.AuthenticationDigits)
                    .SetPositiveButton("Accept",
                        async (sender, args) =>
                        {
                            try
                            {
                                await _platformPair._client.AcceptConnectionAsync(p0, new MyPayloadCallback(_platformPair));
                            }
                            catch (Exception e)
                            {
                                _platformPair.OnErrorOccurred(new ErrorEventArgs
                                {
                                    Exception = e,
                                    Step = "Accept connection"
                                });
                            }
                        }).SetNegativeButton("Cancel",
                        async (sender, args) =>
                        {
                            try
                            {
                                await _platformPair._client.RejectConnectionAsync(p0);
                            }
                            catch (Exception e)
                            {
                                _platformPair.OnErrorOccurred(new ErrorEventArgs
                                {
                                    Exception = e,
                                    Step = "Reject connection"
                                });
                            }
                            _platformPair.Disconnect();
                        }).Show();
            }

            public override void OnConnectionResult(string p0, ConnectionResolution p1)
            {
                //Debug.WriteLine("### OnConnectionResult " + p0 + ", " + p1.Status.StatusMessage);
                if (p1.Status.IsSuccess)
                {
                    _platformPair._connectedPartnerId = p0;
                    _platformPair.OnConnected();
                    _platformPair._client.StopDiscovery();
                    _platformPair._client.StopAdvertising();
                }
                else
                {
                    _platformPair.Disconnect();
                }
            }

            public override void OnDisconnected(string p0)
            {
                Debug.WriteLine("### OnDisconnected " + p0);
                _platformPair.OnDisconnected();
            }
        }

        private class MyPayloadCallback : PayloadCallback
        {
            private readonly PlatformPair _platformPair;
            private byte[] _incomingBytes;
            private int _incomingBytesCounter;
            private int _expectedLength;
            private readonly byte[] _headerBytes = new byte[PairOperator.HEADER_LENGTH];
            private long _payloadId;
            private Stream _payloadStream;

            public MyPayloadCallback(PlatformPair platformPair)
            {
                _platformPair = platformPair;
            }

            public override void OnPayloadReceived(string p0, Payload p1)
            {
                //Debug.WriteLine("### OnPayloadReceived, Id: " + p1.Id);
                _payloadId = p1.Id;

                try
                {
                    _payloadStream = p1.AsStream().AsInputStream();
                    for (var ii = 0; ii < PairOperator.HEADER_LENGTH; ii++)
                    {
                        var nextByte = _payloadStream.ReadByte();
                        _headerBytes[ii] = (byte)nextByte;
                    }
                    _expectedLength = PairOperator.HEADER_LENGTH + ((_headerBytes.ElementAt(3) << 16) | (_headerBytes.ElementAt(4) << 8) | _headerBytes.ElementAt(5));

                    _incomingBytes = new byte[_expectedLength];
                    _incomingBytesCounter = PairOperator.HEADER_LENGTH;
                    for (var ii = 0; ii < _headerBytes.Length; ii++)
                    {
                        _incomingBytes[ii] = _headerBytes[ii];
                    }

                    var readBytes = _payloadStream.Read(_incomingBytes, _incomingBytesCounter, _expectedLength - _incomingBytesCounter);
                    _incomingBytesCounter += readBytes;
                }
                catch (Exception e)
                {
                    _platformPair.OnErrorOccurred(new ErrorEventArgs
                    {
                        Exception = e,
                        Step = "Receive data"
                    });
                }
            }

            public override void OnPayloadTransferUpdate(string p0, PayloadTransferUpdate p1)
            {
                //Debug.WriteLine("### OnPayloadTransferUpdate " + (_mostRecentReceivedPayload?.Id == p1.PayloadId ? "RECEIVE" : "SEND") + " Id: " + p1.PayloadId + " status: " + p1.TransferStatus + " progress: " + p1.BytesTransferred + "/" + _expectedLength);
                var statusName = "";
                switch (p1.TransferStatus)
                {
                    case PayloadTransferUpdate.Status.Canceled:
                        statusName += "Cancelled";
                        break;
                    case PayloadTransferUpdate.Status.Failure:
                        statusName += "Failure";
                        break;
                    case PayloadTransferUpdate.Status.Success:
                        statusName += "Success";
                        break;
                    case PayloadTransferUpdate.Status.InProgress:
                        statusName += "InProgress";
                        break;
                }
                if (p1.TransferStatus == PayloadTransferUpdate.Status.Failure ||
                    p1.TransferStatus == PayloadTransferUpdate.Status.Canceled)
                {
                    _platformPair.OnErrorOccurred(new ErrorEventArgs
                    {
                        Exception = new Exception(
                            (p1.PayloadId == _payloadId ? "receiving " : "sending ") + statusName),
                        Step = "Transfer update"
                    });
                }
                
                if (p1.PayloadId == _payloadId) //only care about receiving
                {
                    int readBytes;
                    switch (p1.TransferStatus)
                    {
                        case PayloadTransferUpdate.Status.InProgress:
                            readBytes = _payloadStream.Read(_incomingBytes, _incomingBytesCounter,
                                _expectedLength - _incomingBytesCounter);
                            _incomingBytesCounter += readBytes;
                            break;
                        case PayloadTransferUpdate.Status.Success:
                            readBytes = _payloadStream.Read(_incomingBytes, _incomingBytesCounter,
                                _expectedLength - _incomingBytesCounter);
                            _incomingBytesCounter += readBytes;

                            _platformPair.ProcessReceivedPayload(_incomingBytes);

                            _payloadStream?.Dispose();
                            break;
                    }
                }
                else
                {
                    if (p1.TransferStatus == PayloadTransferUpdate.Status.Success)
                    {
                    }
                }
            }
        }

        private class MyEndpointDiscoveryCallback : EndpointDiscoveryCallback
        {
            private readonly PlatformPair _pair;

            public MyEndpointDiscoveryCallback(PlatformPair pair)
            {
                _pair = pair;
            }

            public override async void OnEndpointFound(string p0, DiscoveredEndpointInfo p1)
            {
                Debug.WriteLine("### OnEndpointFound " + p0 + ", " + p1.EndpointName);
                if (p1.ServiceId == PairOperator.CROSSCAM_SERVICE)
                {
                    await _pair._client.RequestConnectionAsync(DeviceInfo.Name, p0, new MyConnectionLifecycleCallback(_pair));
                }
            }

            public override void OnEndpointLost(string p0)
            {
                //Debug.WriteLine("### OnEndpointLost " + p0);
            }
        }
    }
}