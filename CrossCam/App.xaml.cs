using System.Collections.Generic;
using CrossCam.ViewModel;
using FreshMvvm;
using Microsoft.AppCenter.Analytics;
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

        protected override void OnStart()
        {
        }

        protected override void OnSleep()
        {
            MessagingCenter.Send(this, APP_PAUSING_EVENT);
        }

        protected override void OnResume()
        {
            MessagingCenter.Send(this, APP_UNPAUSING_EVENT);
        }
    }
}

