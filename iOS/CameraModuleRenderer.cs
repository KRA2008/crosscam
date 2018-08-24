using AVFoundation;
using CoreGraphics;
using CustomRenderer;
using CustomRenderer.iOS;
using Foundation;
using System;
using System.ComponentModel;
using System.Linq;
using UIKit;
using Xamarin.Forms;
using Xamarin.Forms.Platform.iOS;

[assembly:ExportRenderer (typeof(CameraModule), typeof(CameraModuleRenderer))]
namespace CustomRenderer.iOS
{
	public class CameraModuleRenderer : ViewRenderer<ContentView, UIView>
	{
		private AVCaptureSession _captureSession;
	    private AVCaptureDeviceInput _captureDeviceInput;
	    private AVCaptureStillImageOutput _stillImageOutput;
	    private UIView _liveCameraStream;
	    private UIButton _takePhotoButton;
	    private UIButton _toggleCameraButton;
	    private UIButton _toggleFlashButton;
	    private ContentView _contentView;

	    protected override void OnElementPropertyChanged(object sender, PropertyChangedEventArgs e)
	    {
	        base.OnElementPropertyChanged(sender, e);

            if (e.PropertyName == nameof(ContentView.Height) || 
                e.PropertyName == nameof(ContentView.Width))
	        {
	            NativeView.Bounds = new CGRect(0, 0, _contentView.Width, _contentView.Height);
            }

	        if (NativeView.Bounds.Width > 0 && NativeView.Bounds.Height > 0)
	        {
	            SetupUserInterface();
	            SetupEventHandlers();
	            SetupLiveCameraStream();
	            AuthorizeCameraUse();
            }
	    }

	    protected override void OnElementChanged(ElementChangedEventArgs<ContentView> e)
		{
			base.OnElementChanged (e);

			if (e.OldElement != null || Element == null) {
				return;
			}

		    _contentView = e.NewElement;
		}

		protected override void Dispose(bool disposing)
		{
			if (_captureDeviceInput != null)
			{
				_captureSession?.RemoveInput(_captureDeviceInput);
			}

			if(_captureDeviceInput != null)
			{
				_captureDeviceInput.Dispose();
				_captureDeviceInput = null;
			}

			if(_captureSession != null)
			{
				_captureSession.StopRunning();
				_captureSession.Dispose();
				_captureSession = null;
			}

			if (_stillImageOutput != null)
			{
				_stillImageOutput.Dispose();
				_stillImageOutput = null;
			}

			base.Dispose(disposing);
		}

	    private void SetupUserInterface ()
	    {
	        var view = NativeView;
			var centerButtonX = view.Bounds.GetMidX () - 35f;
			var topLeftX = view.Bounds.X + 25;
			var topRightX = view.Bounds.Right - 65;
			var bottomButtonY = view.Bounds.Bottom - 150;
			var topButtonY = view.Bounds.Top + 15;
			const int BUTTON_WIDTH = 70;
			const int BUTTON_HEIGHT = 70;

			_liveCameraStream = new UIView
			{
				Frame = new CGRect (0, 0, view.Bounds.Width, view.Bounds.Height)
			};

			_takePhotoButton = new UIButton
			{
				Frame = new CGRect (centerButtonX, bottomButtonY, BUTTON_WIDTH, BUTTON_HEIGHT)
			};
		    _takePhotoButton.SetBackgroundImage(UIImage.FromFile("TakePhotoButton.png"), UIControlState.Normal);

			_toggleCameraButton = new UIButton
			{
				Frame = new CGRect (topRightX, topButtonY + 5, 35, 26)
			};
		    _toggleCameraButton.SetBackgroundImage(UIImage.FromFile("ToggleCameraButton.png"), UIControlState.Normal);

			_toggleFlashButton = new UIButton
			{
				Frame = new CGRect (topLeftX, topButtonY, 37, 37)
			};
		    _toggleFlashButton.SetBackgroundImage(UIImage.FromFile("NoFlashButton.png"), UIControlState.Normal);

		    view.Add (_liveCameraStream);
		    view.Add (_takePhotoButton);
		    view.Add (_toggleCameraButton);
		    view.Add (_toggleFlashButton);
		}

	    private void SetupEventHandlers ()
		{
			_takePhotoButton.TouchUpInside += (sender, e) => {
				CapturePhoto ();
			};

			_toggleCameraButton.TouchUpInside += (sender, e) => {
				ToggleFrontBackCamera ();
			};

			_toggleFlashButton.TouchUpInside += (sender, e) => {
				ToggleFlash ();
			};
		}

	    private async void CapturePhoto ()
		{
			var videoConnection = _stillImageOutput.ConnectionFromMediaType(AVMediaType.Video);
			var sampleBuffer = await _stillImageOutput.CaptureStillImageTaskAsync(videoConnection);
			var jpegImage = AVCaptureStillImageOutput.JpegStillToNSData(sampleBuffer);

			var photo = new UIImage(jpegImage);
			photo.SaveToPhotosAlbum((image, error) =>
			{
				if (!string.IsNullOrEmpty(error?.LocalizedDescription))
				{
					Console.Error.WriteLine($"\t\t\tError: {error.LocalizedDescription}");
				}
			});
		}

	    private void ToggleFrontBackCamera ()
		{
			var devicePosition = _captureDeviceInput.Device.Position;
		    devicePosition = devicePosition == AVCaptureDevicePosition.Front
		        ? AVCaptureDevicePosition.Back
		        : AVCaptureDevicePosition.Front;

			var device = GetCameraForOrientation (devicePosition);
			ConfigureCameraForDevice (device);

			_captureSession.BeginConfiguration ();
			_captureSession.RemoveInput (_captureDeviceInput);
			_captureDeviceInput = AVCaptureDeviceInput.FromDevice (device);
			_captureSession.AddInput (_captureDeviceInput);
			_captureSession.CommitConfiguration ();
		}

	    private void ToggleFlash ()
		{
			var device = _captureDeviceInput.Device;

			var error = new NSError ();
			if (device.HasFlash) {
				if (device.FlashMode == AVCaptureFlashMode.On) {
					device.LockForConfiguration (out error);
					device.FlashMode = AVCaptureFlashMode.Off;
					device.UnlockForConfiguration ();
				    _toggleFlashButton.SetBackgroundImage(UIImage.FromFile("NoFlashButton.png"), UIControlState.Normal);
				} else {
					device.LockForConfiguration (out error);
					device.FlashMode = AVCaptureFlashMode.On;
					device.UnlockForConfiguration ();
				    _toggleFlashButton.SetBackgroundImage(UIImage.FromFile("FlashButton.png"), UIControlState.Normal);
				}
			}
		}

	    private static AVCaptureDevice GetCameraForOrientation (AVCaptureDevicePosition orientation)
		{
			var devices = AVCaptureDevice.DevicesWithMediaType (AVMediaType.Video);

		    return devices.FirstOrDefault(device => device.Position == orientation);
		}

	    private void SetupLiveCameraStream ()
	    {
			_captureSession = new AVCaptureSession ();

			var viewLayer = _liveCameraStream.Layer;
			var videoPreviewLayer = new AVCaptureVideoPreviewLayer (_captureSession) {
				Frame = _liveCameraStream.Bounds
			};
			_liveCameraStream.Layer.AddSublayer (videoPreviewLayer);

			var captureDevice = AVCaptureDevice.GetDefaultDevice(AVMediaType.Video);
			ConfigureCameraForDevice (captureDevice);
			_captureDeviceInput = AVCaptureDeviceInput.FromDevice (captureDevice);

		    using (var dictionary = new NSMutableDictionary())
		    {
		        dictionary [AVVideo.CodecKey] = new NSNumber ((int)AVVideoCodec.JPEG);
		    }

		    _stillImageOutput = new AVCaptureStillImageOutput
		    {
				OutputSettings = new NSDictionary ()
			};

			_captureSession.AddOutput (_stillImageOutput);
			_captureSession.AddInput (_captureDeviceInput);
			_captureSession.StartRunning ();
		}

	    private static void ConfigureCameraForDevice (AVCaptureDevice device)
		{
			var error = new NSError ();
			if (device.IsFocusModeSupported (AVCaptureFocusMode.ContinuousAutoFocus)) {
				device.LockForConfiguration (out error);
				device.FocusMode = AVCaptureFocusMode.ContinuousAutoFocus;
				device.UnlockForConfiguration ();
			} else if (device.IsExposureModeSupported (AVCaptureExposureMode.ContinuousAutoExposure)) {
				device.LockForConfiguration (out error);
				device.ExposureMode = AVCaptureExposureMode.ContinuousAutoExposure;
				device.UnlockForConfiguration ();
			} else if (device.IsWhiteBalanceModeSupported (AVCaptureWhiteBalanceMode.ContinuousAutoWhiteBalance)) {
				device.LockForConfiguration (out error);
				device.WhiteBalanceMode = AVCaptureWhiteBalanceMode.ContinuousAutoWhiteBalance;
				device.UnlockForConfiguration ();
			}
		}

	    private static async void AuthorizeCameraUse ()
		{
			var authorizationStatus = AVCaptureDevice.GetAuthorizationStatus (AVMediaType.Video);
			if (authorizationStatus != AVAuthorizationStatus.Authorized) {
				await AVCaptureDevice.RequestAccessForMediaTypeAsync (AVMediaType.Video);
			}
		}
	}
}

