using CrossCam.ViewModel;
using FreshMvvm;
using Xamarin.Forms.Xaml;

[assembly: XamlCompilation(XamlCompilationOptions.Compile)]
namespace CrossCam
{
    public partial class App
    {
        public App()
        {
            InitializeComponent();
            MainPage = new FreshNavigationContainer(FreshPageModelResolver.ResolvePageModel<CameraViewModel>());
        }

        protected override void OnStart()
        {
            // Handle when your app starts
        }

        protected override void OnSleep()
        {
            // Handle when your app sleeps
        }

        protected override void OnResume()
        {
            // Handle when your app resumes
        }
    }
}

