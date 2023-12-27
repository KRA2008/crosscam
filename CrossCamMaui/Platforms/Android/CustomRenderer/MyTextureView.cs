using Android.Content;
using Android.Util;
using Android.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrossCam.Platforms.Android.CustomRenderer
{
    internal class MyTextureView : TextureView
    {
        public MyTextureView(Context context, IAttributeSet attrs) : base(context, attrs)
        {
        }

        protected override void OnAttachedToWindow()
        {
            base.OnAttachedToWindow();
            Debug.WriteLine("### HOLY CRAP I'M ATTACHING!");
        }

        protected override void OnDetachedFromWindow()
        {
            base.OnDetachedFromWindow();
        }
    }
}
