using System.ComponentModel;
using System.IO;
using Xamarin.Forms;

namespace CustomRenderer
{
    public sealed class CameraPageViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public ImageSource LeftImageSource { get; set; }
        public byte[] LeftByteArray { get; set; }
        public bool IsLeftCameraVisible { get; set; }
        public ImageSource RightImageSource { get; set; }
        public byte[] RightByteArray { get; set; }
        public bool IsRightCameraVisible { get; set; }

        public CameraPageViewModel()
        {
            IsLeftCameraVisible = true;
            PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(LeftByteArray))
                {
                    LeftImageSource = ImageSource.FromStream(() => new MemoryStream(LeftByteArray));
                    IsLeftCameraVisible = false;
                    IsRightCameraVisible = true;
                }
                else if (args.PropertyName == nameof(RightByteArray))
                {
                    RightImageSource = ImageSource.FromStream(() => new MemoryStream(RightByteArray));
                    IsRightCameraVisible = false;
                }
            };
        }
    }
}