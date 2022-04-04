using CrossCam.ViewModel;
using FreshMvvm;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

[assembly: XamlCompilation(XamlCompilationOptions.Compile)]
namespace CrossCam
{
    public partial class App
    {
        public const string APP_PAUSING_EVENT = "appPausing";
        public const string APP_UNPAUSING_EVENT = "appUnpausing";

        private readonly CameraViewModel _cameraViewModel;

        public App()
        {
            InitializeComponent();
            var cameraPage = FreshPageModelResolver.ResolvePageModel<CameraViewModel>();
            _cameraViewModel = (CameraViewModel)cameraPage.BindingContext;
            MainPage = new FreshNavigationContainer(cameraPage);
        }

        public void LoadSharedImages(byte[] image1, byte[] image2)
        {
            _cameraViewModel.LoadSharedImages(image1, image2);
        }

        protected override void OnStart()
        {
        }

        protected override void OnSleep()
        {
            MessagingCenter.Send(this, APP_PAUSING_EVENT);
            Device.BeginInvokeOnMainThread(() =>
            {
                DeviceDisplay.KeepScreenOn = false;
            });
        }

        protected override void OnResume()
        {
            MessagingCenter.Send(this, APP_UNPAUSING_EVENT);
            Device.BeginInvokeOnMainThread(() =>
            {
                DeviceDisplay.KeepScreenOn = true;
            });
        }
    }
}

