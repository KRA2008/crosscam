using System.Collections.Generic;
using System.Collections.ObjectModel;
using CrossCam.ViewModel;
using Xamarin.Forms;

namespace CrossCam.CustomElement
{
	public sealed partial class CameraModule
	{
		public CameraModule ()
		{
			InitializeComponent ();
	    }

	    public static readonly BindableProperty CapturedImageProperty = BindableProperty.Create(nameof(CapturedImage),
	        typeof(byte[]), typeof(CameraModule), null, BindingMode.OneWayToSource);

        public static readonly BindableProperty CaptureTriggerProperty = BindableProperty.Create(nameof(CaptureTrigger),
            typeof(bool), typeof(CameraModule), false);

	    public static readonly BindableProperty CaptureSuccessProperty = BindableProperty.Create(nameof(CaptureSuccess),
	        typeof(bool), typeof(CameraModule), false, BindingMode.OneWayToSource);

	    public static readonly BindableProperty IsPortraitProperty = BindableProperty.Create(nameof(IsPortrait),
	        typeof(bool), typeof(CameraModule), false, BindingMode.OneWayToSource);

	    public static readonly BindableProperty IsViewInvertedProperty = BindableProperty.Create(nameof(IsViewInverted),
	        typeof(bool), typeof(CameraModule), false, BindingMode.OneWayToSource);

        public static readonly BindableProperty IsTapToFocusEnabledProperty = BindableProperty.Create(nameof(IsTapToFocusEnabled),
	        typeof(bool), typeof(CameraModule), false);

        public static readonly BindableProperty IsLockToFirstEnabledProperty = BindableProperty.Create(nameof(IsLockToFirstEnabled),
            typeof(bool), typeof(CameraModule), true);

        public static readonly BindableProperty SwitchToContinuousFocusTriggerProperty = BindableProperty.Create(nameof(SwitchToContinuousFocusTrigger),
	        typeof(bool), typeof(CameraModule), false);

        public static readonly BindableProperty WasSwipedTriggerProperty = BindableProperty.Create(nameof(WasSwipedTrigger),
            typeof(bool), typeof(CameraModule), false, BindingMode.OneWayToSource);

        public static readonly BindableProperty ErrorMessageProperty = BindableProperty.Create(nameof(ErrorMessage),
	        typeof(string), typeof(CameraModule), null, BindingMode.OneWayToSource);

	    public static readonly BindableProperty IsNothingCapturedProperty = BindableProperty.Create(nameof(IsNothingCaptured),
	        typeof(bool), typeof(CameraModule), true);

	    public static readonly BindableProperty PreviewBottomYProperty = BindableProperty.Create(nameof(PreviewBottomY),
	        typeof(double), typeof(CameraModule), 0d, BindingMode.OneWayToSource);

        public static readonly BindableProperty IsFocusCircleVisibleProperty = BindableProperty.Create(nameof(IsFocusCircleVisible),
            typeof(bool), typeof(CameraModule), false, BindingMode.OneWayToSource);

        public static readonly BindableProperty IsFocusCircleLockedProperty = BindableProperty.Create(nameof(IsFocusCircleLocked),
            typeof(bool), typeof(CameraModule), false, BindingMode.OneWayToSource);

        public static readonly BindableProperty FocusCircleXProperty = BindableProperty.Create(nameof(FocusCircleX),
            typeof(double), typeof(CameraModule), 0d, BindingMode.OneWayToSource);

        public static readonly BindableProperty FocusCircleYProperty = BindableProperty.Create(nameof(FocusCircleY),
            typeof(double), typeof(CameraModule), 0d, BindingMode.OneWayToSource);

        public static readonly BindableProperty BluetoothOperatorProperty = BindableProperty.Create(nameof(BluetoothOperator),
            typeof(BluetoothOperator), typeof(CameraModule));

        public static readonly BindableProperty AvailableCamerasProperty = BindableProperty.Create(nameof(AvailableCameras),
            typeof(ObservableCollection<AvailableCamera>), typeof(CameraModule), defaultBindingMode:BindingMode.TwoWay);

        public static readonly BindableProperty ChosenCameraProperty = BindableProperty.Create(nameof(ChosenCamera),
            typeof(AvailableCamera), typeof(CameraModule), defaultBindingMode:BindingMode.TwoWay);

        public byte[] CapturedImage
        {
            get => (byte[])GetValue(CapturedImageProperty);
            set => SetValue(CapturedImageProperty, value);
        }

        public bool CaptureTrigger
        {
            get => (bool)GetValue(CaptureTriggerProperty);
            set => SetValue(CaptureTriggerProperty, value);
        }

	    public bool CaptureSuccess
	    {
	        get => (bool)GetValue(CaptureSuccessProperty);
	        set => SetValue(CaptureSuccessProperty, value);
	    }

	    public bool IsPortrait
	    {
	        get => (bool)GetValue(IsPortraitProperty);
	        set => SetValue(IsPortraitProperty, value);
	    }

	    public bool IsViewInverted
        {
	        get => (bool)GetValue(IsViewInvertedProperty);
	        set => SetValue(IsViewInvertedProperty, value);
	    }

        public bool IsTapToFocusEnabled
	    {
	        get => (bool)GetValue(IsTapToFocusEnabledProperty);
	        set => SetValue(IsTapToFocusEnabledProperty, value);
	    }

        public bool IsLockToFirstEnabled
        {
            get => (bool)GetValue(IsLockToFirstEnabledProperty);
            set => SetValue(IsLockToFirstEnabledProperty, value);
        }

        public bool SwitchToContinuousFocusTrigger
        {
	        get => (bool)GetValue(SwitchToContinuousFocusTriggerProperty);
	        set => SetValue(SwitchToContinuousFocusTriggerProperty, value);
	    }
        
        public bool WasSwipedTrigger
        {
            get => (bool)GetValue(WasSwipedTriggerProperty);
            set => SetValue(WasSwipedTriggerProperty, value);
        }

        public string ErrorMessage
	    {
	        get => (string)GetValue(ErrorMessageProperty);
	        set => SetValue(ErrorMessageProperty, value);
	    }

	    public bool IsNothingCaptured
	    {
	        get => (bool)GetValue(IsNothingCapturedProperty);
	        set => SetValue(IsNothingCapturedProperty, value);
	    }

	    public double PreviewBottomY
	    {
	        get => (double) GetValue(PreviewBottomYProperty);
	        set => SetValue(PreviewBottomYProperty, value);
        }

        public bool IsFocusCircleVisible
        {
            get => (bool)GetValue(IsFocusCircleVisibleProperty);
            set => SetValue(IsFocusCircleVisibleProperty, value);
        }

        public bool IsFocusCircleLocked
        {
            get => (bool)GetValue(IsFocusCircleLockedProperty);
            set => SetValue(IsFocusCircleLockedProperty, value);
        }

        public double FocusCircleX
        {
            get => (double)GetValue(FocusCircleXProperty);
            set => SetValue(FocusCircleXProperty, value);
        }

        public double FocusCircleY
        {
            get => (double)GetValue(FocusCircleYProperty);
            set => SetValue(FocusCircleYProperty, value);
        }

        public BluetoothOperator BluetoothOperator
        {
            get => (BluetoothOperator)GetValue(BluetoothOperatorProperty);
            set => SetValue(BluetoothOperatorProperty, value);
        }

        public ObservableCollection<AvailableCamera> AvailableCameras
        {
            get => (ObservableCollection<AvailableCamera>)GetValue(AvailableCamerasProperty);
            set => SetValue(AvailableCamerasProperty, value);
        }

        public AvailableCamera ChosenCamera
        {
            get => (AvailableCamera)GetValue(ChosenCameraProperty);
            set => SetValue(ChosenCameraProperty, value);
        }
    }
}