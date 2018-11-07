using SkiaSharp.Views.Forms;
using Xamarin.Forms;

namespace CrossCam.CustomElement
{
    public class CanvasView : SKCanvasView
    {
        public static readonly BindableProperty BorderThicknessProperty = BindableProperty.Create(nameof(BorderThickness),
            typeof(int), typeof(CanvasView), 0, BindingMode.TwoWay);

        public static readonly BindableProperty LeftImageLeftCropProperty = BindableProperty.Create(nameof(LeftImageLeftCrop),
            typeof(int), typeof(CanvasView), 0, BindingMode.TwoWay);

        public static readonly BindableProperty LeftImageRightCropProperty = BindableProperty.Create(nameof(LeftImageRightCrop),
            typeof(int), typeof(CanvasView), 0, BindingMode.TwoWay);

        public static readonly BindableProperty RightImageLeftCropProperty = BindableProperty.Create(nameof(RightImageLeftCrop),
            typeof(int), typeof(CanvasView), 0, BindingMode.TwoWay);

        public static readonly BindableProperty RightImageRightCropProperty = BindableProperty.Create(nameof(RightImageRightCrop),
            typeof(int), typeof(CanvasView), 0, BindingMode.TwoWay);

        public int BorderThickness
        {
            get => (int)GetValue(BorderThicknessProperty);
            set => SetValue(BorderThicknessProperty, value);
        }

        public int LeftImageLeftCrop
        {
            get => (int)GetValue(LeftImageLeftCropProperty);
            set => SetValue(LeftImageLeftCropProperty, value);
        }

        public int LeftImageRightCrop
        {
            get => (int)GetValue(LeftImageRightCropProperty);
            set => SetValue(LeftImageRightCropProperty, value);
        }

        public int RightImageLeftCrop
        {
            get => (int)GetValue(RightImageLeftCropProperty);
            set => SetValue(RightImageLeftCropProperty, value);
        }

        public int RightImageRightCrop
        {
            get => (int)GetValue(RightImageRightCropProperty);
            set => SetValue(RightImageRightCropProperty, value);
        }
    }
}