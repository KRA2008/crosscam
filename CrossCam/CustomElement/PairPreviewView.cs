using CrossCam.Wrappers;
using Xamarin.Forms;

namespace CrossCam.CustomElement
{
    public class PairPreviewView : ContentView
    {
        public static readonly BindableProperty BluetoothOperatorProperty = BindableProperty.Create(nameof(PairOperator),
            typeof(PairOperator), typeof(CameraModule));

        public PairOperator PairOperator
        {
            get => (PairOperator)GetValue(BluetoothOperatorProperty);
            set => SetValue(BluetoothOperatorProperty, value);
        }
    }
}