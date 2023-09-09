using CrossCam.Model;

namespace CrossCam.ViewModel
{
    public class FaqViewModel : BaseViewModel
    {
        public FaqScrollOptions RequestedScrollOption { get; set; }

        public override void Init(object initData)
        {
            base.Init(initData);
            if (initData is FaqScrollOptions option)
            {
                RequestedScrollOption = option;
            }
        }
    }
}