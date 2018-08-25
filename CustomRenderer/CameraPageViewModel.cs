using System.ComponentModel;
using System.IO;
using Xamarin.Forms;

namespace CustomRenderer
{
    public sealed class CameraPageViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public ImageSource LeftImageSource { get; set; }
        private byte[] LeftByteArray { get; set; }
        public ImageSource RightImageSource { get; set; }
        private byte[] RightByteArray { get; set; }
        public byte[] IncomingByteArray { get; set; }
        public int CameraModuleColumn { get; set; }
        public bool IsCameraModuleVisible { get; set; }

        public CameraPageViewModel()
        {
            IsCameraModuleVisible = true;
            PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(IncomingByteArray) && IncomingByteArray != null)
                {
                    if (CameraModuleColumn == 0)
                    {
                        LeftByteArray = IncomingByteArray;
                        LeftImageSource = ImageSource.FromStream(() => new MemoryStream(LeftByteArray));
                        CameraModuleColumn = 1;
                    }
                    else if (CameraModuleColumn == 1)
                    {
                        RightByteArray = IncomingByteArray;
                        RightImageSource = ImageSource.FromStream(() => new MemoryStream(RightByteArray));
                        IsCameraModuleVisible = false;
                    }
                }
            };
        }
    }
}