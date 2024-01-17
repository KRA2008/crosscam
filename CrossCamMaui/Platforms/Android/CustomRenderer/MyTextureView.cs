using Android.Content;
using Android.Graphics.Drawables;
using Android.Runtime;
using Android.Util;
using Android.Views;

namespace CrossCam.Platforms.Android.CustomRenderer
{
    public class MyTextureView : TextureView
    {
        protected MyTextureView(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        public MyTextureView(Context context, IAttributeSet? attrs, int defStyleAttr, int defStyleRes) : base(context, attrs, defStyleAttr, defStyleRes)
        {
        }

        public MyTextureView(Context context, IAttributeSet? attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
        {
        }

        public MyTextureView(Context context, IAttributeSet? attrs) : base(context, attrs)
        {
        }

        public MyTextureView(Context context) : base(context)
        {
        }

        public override void SetBackgroundDrawable(Drawable? background)
        {
            // FORBIDDEN.
        }
    }
}
