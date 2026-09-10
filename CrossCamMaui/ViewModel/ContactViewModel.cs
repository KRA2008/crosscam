namespace CrossCam.ViewModel
{
    public class ContactViewModel : BaseViewModel
    {
        public Command OpenTestingPage { get; set; }

        public ContactViewModel()
        {
            OpenTestingPage = new Command(url =>
            {
                OpenLink.Execute(DeviceInfo.Current.Platform == DevicePlatform.Android ?
                    "https://play.google.com/apps/testing/com.kra2008.crosscam" :
                    "https://testflight.apple.com/join/BmZlX08e");
            });
        }
    }
}