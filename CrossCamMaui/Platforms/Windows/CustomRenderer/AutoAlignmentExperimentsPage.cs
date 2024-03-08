using System.Diagnostics;
using System.Reflection;
using CrossCam.Model;
using CrossCam.Wrappers;
using Microsoft.Maui.Layouts;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace CrossCam.Platforms.Windows.CustomRenderer;

public class AutoAlignmentExperimentsPage : ContentPage
{
    private SKCanvasView _canvas;
    private AlignedResult _alignedResult;
    private Label _pointsCount;
    private DisplayMode _displayMode;

    public AutoAlignmentExperimentsPage()
    {
        _canvas = new SKCanvasView
        {
            BackgroundColor = Colors.Blue
        };
        _canvas.PaintSurface += CanvasOnPaintSurface;
        AbsoluteLayout.SetLayoutFlags(_canvas, AbsoluteLayoutFlags.All);
        AbsoluteLayout.SetLayoutBounds(_canvas, new Rect(0, 0, 1, 1));

        var button = new Button
        {
            BackgroundColor = Colors.Yellow,
            Text = "Toggle"
        };
        button.Clicked += Button_OnClicked;
        AbsoluteLayout.SetLayoutFlags(button, AbsoluteLayoutFlags.PositionProportional);
        AbsoluteLayout.SetLayoutBounds(button, new Rect(1, 1, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));

        _pointsCount = new Label
        {
            TextColor = Colors.Green
        };
        AbsoluteLayout.SetLayoutFlags(_pointsCount, AbsoluteLayoutFlags.PositionProportional);
        AbsoluteLayout.SetLayoutBounds(_pointsCount, new Rect(0,0, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));

        Content = new AbsoluteLayout
        {
            BackgroundColor = Colors.Green,
            Children =
            {
                _canvas,
                button,
                _pointsCount
            }
        };
    }

    private void CanvasOnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        Debug.WriteLine("WHAT IS GOING ON?");
        if (_displayMode == DisplayMode.DirtyMatches ||
            _displayMode == DisplayMode.CleanMatches)
        {
            var bitmapToDraw = _displayMode == DisplayMode.DirtyMatches
                ? _alignedResult.DrawnDirtyMatches
                : _alignedResult.DrawnCleanMatches;
            var aspectRatio = bitmapToDraw.Width / (bitmapToDraw.Height * 1f);
            var matchesWidth = Height * aspectRatio;
            e.Surface.Canvas.DrawBitmap(bitmapToDraw,
                new SKRect(0, 0, (float)matchesWidth, (float)Height));
            _pointsCount.Text = _displayMode == DisplayMode.DirtyMatches
                ? _alignedResult.DirtyMatchesCount.ToString()
                : _alignedResult.CleanMatchesCount.ToString();
        }
        else
        {
            var aspectRatio = _alignedResult.Warped1.Width / (_alignedResult.Warped1.Height * 1f);
            var aspectFillWidth = Height * aspectRatio;
            e.Surface.Canvas.DrawBitmap(_alignedResult.Warped1,
                new SKRect(0, 0, (float)aspectFillWidth, (float)Height));
            e.Surface.Canvas.DrawBitmap(_alignedResult.Warped2,
                new SKRect((float)aspectFillWidth, 0, (float)(2*aspectFillWidth), (float)Height));
        }
    }

    protected override async void OnAppearing()
    {
        try
        {
            base.OnAppearing();

            var autoAlignment = new OpenCv();
            var assembly = GetType().GetTypeInfo().Assembly;
            var resourceBase = "CrossCam.Platforms.Windows.Resources.moiraine";
            await using var leftStream = assembly.GetManifestResourceStream(resourceBase + "left.JPG");
            await using var rightStream = assembly.GetManifestResourceStream(resourceBase + "rightunaligned.JPG");
            using var leftBitmap = SKBitmap.Decode(leftStream);
            using var rightBitmap = SKBitmap.Decode(rightStream);

            var alignmentSettings = new AlignmentSettings();
            alignmentSettings.ResetToDefaults();
            alignmentSettings.DrawKeypointMatches = true;
            alignmentSettings.DiscardOutliersByDistance = true;
            alignmentSettings.DiscardOutliersBySlope1 = true;
            alignmentSettings.PhysicalDistanceThreshold = 0.3f;

            _alignedResult = autoAlignment.ComboAlign(leftBitmap, rightBitmap, alignmentSettings);

            if (_alignedResult == null) throw new Exception("the alignment failed.");

            MainThread.BeginInvokeOnMainThread(() =>
            {
                _pointsCount.Text = _alignedResult.DirtyMatchesCount.ToString();
                _canvas.InvalidateSurface();
            });
        }
        catch (Exception e)
        {
            Debugger.Break();
        }
    }

    private void Button_OnClicked(object sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _displayMode += 1;
            if (_displayMode == (DisplayMode)4) _displayMode = 0;
            _canvas.InvalidateSurface();
        });
    }

    private enum DisplayMode
    {
        DirtyMatches,
        CleanMatches,
        Aligned
    }
}