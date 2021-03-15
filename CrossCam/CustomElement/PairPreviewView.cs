using CrossCam.Wrappers;
using Xamarin.Forms;

namespace CrossCam.CustomElement
{
    public class PairPreviewView : ContentView
    {
        public static readonly BindableProperty BluetoothOperatorProperty = BindableProperty.Create(nameof(BluetoothOperator),
            typeof(BluetoothOperator), typeof(CameraModule));

        public BluetoothOperator BluetoothOperator
        {
            get => (BluetoothOperator)GetValue(BluetoothOperatorProperty);
            set => SetValue(BluetoothOperatorProperty, value);
        }
    }
}