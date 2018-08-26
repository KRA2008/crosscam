using FreshMvvm;

namespace CustomRenderer.ViewModel
{
    public class RenderViewModel : FreshBasePageModel
    {
        public byte[] LeftImage;
        public byte[] RightImage;

        public override void Init(object initData)
        {
            base.Init(initData);

            var images = (byte[][]) initData;
            LeftImage = images[0];
            RightImage = images[1];
        }
    }
}