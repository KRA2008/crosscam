#if __WINDOWS__
using CrossCam.Platforms.Windows.CustomRenderer;
#else
using FreshMvvm.Maui;
#endif
using CrossCam.Model;
using CrossCam.ViewModel;
using CrossCam.Wrappers;
using System.Globalization;
using CrossCam.Resources.Localization;
using NewRelic.MAUI.Plugin;
using CommunityToolkit.Mvvm.Messaging;

[assembly: XamlCompilation(XamlCompilationOptions.Compile)]
namespace CrossCam
{
    public partial class App
    {
        private static CameraViewModel _cameraViewModel;

        public App()
        {
            InitializeComponent();

            var settings = PersistentStorage.LoadOrDefault(PersistentStorage.SETTINGS_KEY, new Settings());
            if (settings.ForceEnglish)
            {
                CultureInfo.CurrentUICulture = new CultureInfo("en-US", false);
                AppResources.Culture = CultureInfo.CurrentUICulture;
            }
#if !__WINDOWS__
            var cameraPage = FreshPageModelResolver.ResolvePageModel<CameraViewModel>();
            _cameraViewModel = (CameraViewModel)cameraPage.BindingContext;
            MainPage = new FreshNavigationContainer(cameraPage);
#else
            MainPage = new AutoAlignmentExperimentsPage();
#endif
        }

        public static void LoadSharedImages(byte[] image1, byte[] image2)
        {
            _cameraViewModel.LoadSharedImages(image1, image2);
        }

        protected override void OnSleep()
        {
            WeakReferenceMessenger.Default.Send(new AppPauseChangedMessage(true));
            base.OnSleep();
        }

        protected override void OnResume()
        {
            base.OnResume();
            WeakReferenceMessenger.Default.Send(new AppPauseChangedMessage(false));
        }
    }
}

