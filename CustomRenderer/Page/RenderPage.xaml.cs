using System.IO;
using CustomRenderer.ViewModel;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using Xamarin.Forms;

namespace CustomRenderer.Page
{
    // ReSharper disable once UnusedMember.Global
	public partial class RenderPage
	{
	    private SKBitmap _leftBitmap;
	    private SKBitmap _rightBitmap;

	    public RenderPage()
	    {
	        var canvasView = new SKCanvasView
	        {
                VerticalOptions = LayoutOptions.FillAndExpand
	        };
	        canvasView.PaintSurface += OnCanvasViewPaintSurface;
	        var button = new Button
	        {
                Text = "Save",
                TextColor = Color.White,
                Margin = 10,
	            Command = new Command(() =>
	            {

	            })
	        };
	        Content = new StackLayout
	        {
	            Children =
	            {
	                canvasView,
                    button
	            }
	        };
	        InitializeComponent();
	    }

	    protected override void OnBindingContextChanged()
	    {
	        base.OnBindingContextChanged();

	        var viewModel = (RenderViewModel) BindingContext;

	        if (viewModel != null)
	        {
	            if (viewModel.LeftImage != null)
	            {
	                using (var leftStream = new MemoryStream(viewModel.LeftImage))
	                {
	                    _leftBitmap = ReorientVertically(SKBitmap.Decode(leftStream));
	                }
	            }

	            if (viewModel.RightImage != null)
	            {
	                using (var rightStream = new MemoryStream(viewModel.RightImage))
	                {
	                    _rightBitmap = ReorientVertically(SKBitmap.Decode(rightStream));
	                }
	            }
	        }
        }

	    private static SKBitmap ReorientVertically(SKBitmap originalBitmap)
	    {
	        var rotated = new SKBitmap(originalBitmap.Height, originalBitmap.Width);

	        using (var surface = new SKCanvas(rotated))
	        {
	            surface.Translate(rotated.Width, 0);
	            surface.RotateDegrees(90);
	            surface.DrawBitmap(originalBitmap, 0, 0);
	        }

	        return rotated;
        }

	    private static SKBitmap Reorient180(SKBitmap originalBitmap)
	    {
	        using (var surface = new SKCanvas(originalBitmap))
	        {
	            surface.RotateDegrees(180, originalBitmap.Width / 2f, originalBitmap.Height / 2f);
	            surface.DrawBitmap(originalBitmap.Copy(), 0, 0);
	        }

	        return originalBitmap;
        }

	    private void OnCanvasViewPaintSurface(object sender, SKPaintSurfaceEventArgs args)
	    {
	        var info = args.Info;
	        var surface = args.Surface;
	        var canvas = surface.Canvas;

	        canvas.Clear();

	        float aspectRatio;
	        float scaledHeight;
	        float margin;

	        var doWeHaveAutomaticOrientation = false;
	        if (doWeHaveAutomaticOrientation)
	        {
	            aspectRatio = _leftBitmap.Height / (_leftBitmap.Width * 1f);
	            scaledHeight = aspectRatio * info.Width;
	            margin = info.Height - scaledHeight * 2;

	            if (_leftBitmap != null &&
	                _rightBitmap != null)
	            {
	                canvas.DrawBitmap(_leftBitmap, new SKRect(0, margin / 2f, info.Width, scaledHeight + margin / 2f));
	                canvas.DrawBitmap(_rightBitmap,
	                    new SKRect(0, scaledHeight + margin / 2f, info.Width, scaledHeight * 2f + margin / 2f));
	            }
            }
	        else
	        {
                aspectRatio = _leftBitmap.Height / (_leftBitmap.Width * 1f);
                scaledHeight = aspectRatio * info.Width / 2f;
	            margin = (info.Height - scaledHeight) / 2;
	            if (_leftBitmap != null &&
	                _rightBitmap != null)
	            {
	                canvas.DrawBitmap(_leftBitmap, new SKRect(0, margin, info.Width / 2f, scaledHeight + margin));
	                canvas.DrawBitmap(_rightBitmap,
	                    new SKRect(info.Width / 2f, margin, info.Width, scaledHeight + margin));
	            }
            }
        }
	}
}