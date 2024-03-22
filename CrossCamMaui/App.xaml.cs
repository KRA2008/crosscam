using System.Diagnostics;
using CrossCam.ViewModel;
using FreshMvvm.Maui;
using Microsoft.AppCenter.Analytics;

[assembly: XamlCompilation(XamlCompilationOptions.Compile)]
namespace CrossCam
{
    public partial class App
    {
        public const string APP_PAUSING_EVENT = "appPausing";
        public const string APP_UNPAUSING_EVENT = "appUnpausing";
        public static bool IsAnalyticsInDebugMode = false;

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

        public static void SendDebugEvent(string moment, string details = null)
        {
            if (IsAnalyticsInDebugMode)
            {
                var dictionary = new Dictionary<string, string>()
                {
                    {"moment", moment},
                    {"details", details}
                };
                Analytics.TrackEvent("DEBUG", dictionary);
            }
        }

        protected override void OnSleep()
        {
            MessagingCenter.Send(this, APP_PAUSING_EVENT);
            base.OnSleep();
        }

        protected override void OnResume()
        {
            base.OnResume();
            MessagingCenter.Send(this, APP_UNPAUSING_EVENT);
        }
    }
}

