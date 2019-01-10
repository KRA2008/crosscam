using CrossCam.ViewModel;
using FreshMvvm;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

[assembly: XamlCompilation(XamlCompilationOptions.Compile)]
namespace CrossCam
{
    public partial class App
    {
        public const string APP_PAUSING_EVENT = "appPausing";
        public const string APP_UNPAUSING_EVENT = "appUnpausing";

        public App()
        {
            InitializeComponent();
            MainPage = new FreshNavigationContainer(FreshPageModelResolver.ResolvePageModel<CameraViewModel>());
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

