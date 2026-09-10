using CrossCam.Model;

namespace CrossCam.ViewModel
{
    public class FaqViewModel : BaseViewModel
    {
        public FaqScrollOptions RequestedScrollOption { get; set; }
        public Command OpenTestingPage { get; set; }

        public override void Init(object initData)
        {
            base.Init(initData);
            if (initData is FaqScrollOptions option)
            {
                RequestedScrollOption = option;
            }

            OpenTestingPage = new Command(url =>
            {
                OpenLink.Execute(DeviceInfo.Current.Platform == DevicePlatform.Android ? 
                    "https://play.google.com/apps/testing/com.kra2008.crosscam" : 
                    "https://testflight.apple.com/join/BmZlX08e");
            });
        }
    }
}